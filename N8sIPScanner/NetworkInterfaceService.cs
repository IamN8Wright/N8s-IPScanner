using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace N8sIPScanner;

public static class NetworkInterfaceService
{
    public static List<NetworkInterfaceInfo> GetActiveIPv4Interfaces(bool includeLoopbackAdapters = false, bool includeDisconnectedAdapters = false)
    {
        var results = new List<NetworkInterfaceInfo>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            if (!includeLoopbackAdapters && nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            if (!includeDisconnectedAdapters && nic.OperationalStatus != OperationalStatus.Up)
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

            var ipv4Properties = TryGetIPv4Properties(properties);

            var dnsAddresses = properties.DnsAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                .Select(d => d.ToString())
                .ToList();

            var gateway = properties.GatewayAddresses
                .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(g => g.Address.ToString())
                .DefaultIfEmpty("None")
                .Aggregate((a, b) => $"{a}, {b}");

            if (string.IsNullOrWhiteSpace(gateway))
            {
                gateway = "None";
            }

            var mac = FormatMacAddress(nic.GetPhysicalAddress());

            var ipv4Addresses = properties.UnicastAddresses
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            if (ipv4Addresses.Count == 0)
            {
                results.Add(new NetworkInterfaceInfo
                {
                    InterfaceName = nic.Name,
                    AdapterName = nic.Description,
                    IPv4Address = "No IPv4",
                    SubnetMask = "Unknown",
                    AddressMethod = "No IPv4",
                    Gateway = gateway,
                    PrimaryDns = dnsAddresses.Count > 0 ? dnsAddresses[0] : "",
                    SecondaryDns = dnsAddresses.Count > 1 ? dnsAddresses[1] : "",
                    MacAddress = mac,
                    SubnetPrefix = "",
                    OperationalStatus = nic.OperationalStatus.ToString(),
                    InterfaceType = nic.NetworkInterfaceType.ToString(),
                    HasIPv4 = false,
                    IsApipa = false
                });

                continue;
            }

            foreach (var unicast in ipv4Addresses)
            {
                var ip = unicast.Address;
                var mask = unicast.IPv4Mask;
                var ipText = ip.ToString();
                var isApipa = ipText.StartsWith("169.254.", StringComparison.Ordinal);
                var addressMethod = GetAddressMethod(ipv4Properties, isApipa);

                results.Add(new NetworkInterfaceInfo
                {
                    InterfaceName = nic.Name,
                    AdapterName = nic.Description,
                    IPv4Address = ipText,
                    SubnetMask = mask?.ToString() ?? "Unknown",
                    AddressMethod = addressMethod,
                    Gateway = gateway,
                    PrimaryDns = dnsAddresses.Count > 0 ? dnsAddresses[0] : "",
                    SecondaryDns = dnsAddresses.Count > 1 ? dnsAddresses[1] : "",
                    MacAddress = mac,
                    SubnetPrefix = GetSubnetPrefix(ip),
                    OperationalStatus = nic.OperationalStatus.ToString(),
                    InterfaceType = nic.NetworkInterfaceType.ToString(),
                    HasIPv4 = true,
                    IsApipa = isApipa
                });
            }
        }

        return results
            .OrderBy(r => GetStatusRank(r.OperationalStatus))
            .ThenBy(r => GetTypeRank(r.InterfaceType))
            .ThenBy(r => r.InterfaceName)
            .ThenBy(r => r.IPv4Address)
            .ToList();
    }

    private static IPv4InterfaceProperties? TryGetIPv4Properties(IPInterfaceProperties properties)
    {
        try
        {
            return properties.GetIPv4Properties();
        }
        catch
        {
            return null;
        }
    }

    private static string GetAddressMethod(IPv4InterfaceProperties? ipv4Properties, bool isApipa)
    {
        if (isApipa)
        {
            return "APIPA / No DHCP";
        }

        if (ipv4Properties is null)
        {
            return "IPv4";
        }

        return ipv4Properties.IsDhcpEnabled ? "DHCP" : "Static";
    }

    private static int GetStatusRank(string status)
    {
        return status switch
        {
            "Up" => 0,
            "Dormant" => 1,
            "LowerLayerDown" => 2,
            "Down" => 3,
            "NotPresent" => 4,
            _ => 5
        };
    }

    private static int GetTypeRank(string type)
    {
        return type switch
        {
            "Ethernet" => 0,
            "GigabitEthernet" => 0,
            "FastEthernetT" => 0,
            "Wireless80211" => 1,
            "Loopback" => 8,
            _ => 5
        };
    }

    private static string GetSubnetPrefix(IPAddress ipAddress)
    {
        var parts = ipAddress.ToString().Split('.');
        return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : "";
    }

    private static string FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 0 ? "Unknown" : string.Join(":", bytes.Select(b => b.ToString("X2")));
    }
}
