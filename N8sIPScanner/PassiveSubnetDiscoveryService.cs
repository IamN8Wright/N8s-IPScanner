using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace N8sIPScanner;

public static class PassiveSubnetDiscoveryService
{
    private sealed class ListenTarget
    {
        public int Port { get; init; }
        public string Protocol { get; init; } = "";
        public string? MulticastAddress { get; init; }
    }

    private static readonly ListenTarget[] ListenTargets =
    {
        new() { Port = 5353, Protocol = "mDNS", MulticastAddress = "224.0.0.251" },
        new() { Port = 1900, Protocol = "SSDP", MulticastAddress = "239.255.255.250" },
        new() { Port = 3702, Protocol = "WS-Discovery", MulticastAddress = "239.255.255.250" },
        new() { Port = 5355, Protocol = "LLMNR", MulticastAddress = "224.0.0.252" },
        new() { Port = 137, Protocol = "NetBIOS", MulticastAddress = null },
        new() { Port = 68, Protocol = "DHCP", MulticastAddress = null }
    };

    public static async Task ListenAsync(
        NetworkInterfaceInfo? selectedInterface,
        Action<PassiveDiscoveryObservation> onObservation,
        CancellationToken cancellationToken)
    {
        var localAddresses = GetLocalIPv4Addresses(selectedInterface).ToList();
        var localAddressStrings = new HashSet<string>(
            localAddresses.Select(ip => ip.ToString()),
            StringComparer.OrdinalIgnoreCase);

        var tasks = ListenTargets
            .Select(target => Task.Run(
                () => ListenLoop(target, localAddresses, localAddressStrings, onObservation, cancellationToken),
                CancellationToken.None))
            .ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Normal stop path.
        }
    }

    private static IEnumerable<IPAddress> GetLocalIPv4Addresses(NetworkInterfaceInfo? selectedInterface)
    {
        if (selectedInterface is not null &&
            IPAddress.TryParse(selectedInterface.IPv4Address, out var selectedIp) &&
            selectedIp.AddressFamily == AddressFamily.InterNetwork)
        {
            yield return selectedIp;
        }

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            IPInterfaceProperties properties;

            try
            {
                properties = nic.GetIPProperties();
            }
            catch
            {
                continue;
            }

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(unicast.Address))
                {
                    yield return unicast.Address;
                }
            }
        }
    }

    private static void ListenLoop(
        ListenTarget target,
        IReadOnlyList<IPAddress> localAddresses,
        HashSet<string> localAddressStrings,
        Action<PassiveDiscoveryObservation> onObservation,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new UdpClient(AddressFamily.InterNetwork);
            client.ExclusiveAddressUse = false;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, target.Port));
            client.Client.ReceiveTimeout = 1000;

            TryJoinMulticastGroups(client, target, localAddresses);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    var payload = client.Receive(ref remote);

                    if (!IsUsefulSource(remote.Address, localAddressStrings))
                    {
                        continue;
                    }

                    var observation = CreateObservation(remote.Address, target.Protocol, payload);
                    if (!string.IsNullOrWhiteSpace(observation.SuggestedCidr))
                    {
                        onObservation(observation);
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    // Poll again so cancellation can be observed.
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch
                {
                    // One malformed packet should not stop discovery.
                }
            }
        }
        catch
        {
            // Some ports may already be bound by Windows services or blocked by policy.
            // Passive discovery continues with whichever listeners are available.
        }
    }

    private static void TryJoinMulticastGroups(UdpClient client, ListenTarget target, IReadOnlyList<IPAddress> localAddresses)
    {
        if (string.IsNullOrWhiteSpace(target.MulticastAddress) ||
            !IPAddress.TryParse(target.MulticastAddress, out var groupAddress))
        {
            return;
        }

        var joined = false;

        foreach (var localAddress in localAddresses)
        {
            try
            {
                client.JoinMulticastGroup(groupAddress, localAddress);
                joined = true;
            }
            catch
            {
                // Try the next local address.
            }
        }

        if (!joined)
        {
            try
            {
                client.JoinMulticastGroup(groupAddress);
            }
            catch
            {
                // Listener can still receive normal broadcasts on this port.
            }
        }
    }

    private static bool IsUsefulSource(IPAddress address, HashSet<string> localAddressStrings)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        var value = address.ToString();

        if (value == "0.0.0.0" || value == "255.255.255.255")
        {
            return false;
        }

        if (localAddressStrings.Contains(value))
        {
            return false;
        }

        return true;
    }

    private static PassiveDiscoveryObservation CreateObservation(IPAddress sourceIp, string protocol, byte[] payload)
    {
        var octets = sourceIp.GetAddressBytes();

        if (octets.Length != 4)
        {
            return new PassiveDiscoveryObservation();
        }

        var cidr = $"{octets[0]}.{octets[1]}.{octets[2]}.0/24";
        var suggestedHost = PickSuggestedHost(octets[3]);
        var suggestedIp = $"{octets[0]}.{octets[1]}.{octets[2]}.{suggestedHost}";
        var details = BuildDetails(protocol, payload);

        return new PassiveDiscoveryObservation
        {
            Timestamp = DateTimeOffset.Now,
            SourceIp = sourceIp.ToString(),
            Protocol = protocol,
            SuggestedCidr = cidr,
            SuggestedIp = suggestedIp,
            SuggestedMask = "255.255.255.0",
            Details = details
        };
    }

    private static int PickSuggestedHost(byte observedHost)
    {
        // Choose a high host address that is commonly free for temporary field work.
        // Avoid the observed host and avoid .0/.1/.254/.255.
        var preferred = new[] { 250, 251, 249, 248, 247, 246, 245 };

        foreach (var host in preferred)
        {
            if (host != observedHost)
            {
                return host;
            }
        }

        return 250;
    }

    private static string BuildDetails(string protocol, byte[] payload)
    {
        var text = "";

        try
        {
            text = Encoding.ASCII.GetString(payload)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\0', ' ')
                .Trim();

            text = new string(text.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());

            while (text.Contains("  ", StringComparison.Ordinal))
            {
                text = text.Replace("  ", " ", StringComparison.Ordinal);
            }

            if (text.Length > 90)
            {
                text = text[..90] + "...";
            }
        }
        catch
        {
            text = "";
        }

        var baseNote = protocol switch
        {
            "mDNS" => "Multicast name traffic heard.",
            "SSDP" => "UPnP/SSDP discovery traffic heard.",
            "WS-Discovery" => "WS-Discovery traffic heard.",
            "LLMNR" => "Local name-resolution traffic heard.",
            "NetBIOS" => "NetBIOS broadcast traffic heard.",
            "DHCP" => "DHCP/broadcast traffic heard.",
            _ => "Broadcast/multicast traffic heard."
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return baseNote + " Socket passive mode cannot see every packet Windows filters out. Mask cannot be proven passively; /24 is a starting guess.";
        }

        return $"{baseNote} {text} Socket passive mode cannot see every packet Windows filters out. Mask cannot be proven passively; /24 is a starting guess.";
    }
}
