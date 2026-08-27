using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace N8sIPScanner;

public sealed class IpScanner
{
    private static readonly Regex ArpRegex = new(
        @"^\s*(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9a-fA-F-]{17})\s+",
        RegexOptions.Compiled);

    // Reliability-first scan pressure. This is deliberately much quieter than the
    // fast 2.3.x profiles so slower embedded/AV devices have time to answer.
    private static readonly SemaphoreSlim DiscoveryGate = new(6, 6);

    // The older scanner behavior effectively allowed hostname resolution to finish
    // instead of racing many short DNS/NetBIOS lookups. Serialize reverse DNS to
    // reproduce that behavior without blocking offline addresses indefinitely.
    private static readonly SemaphoreSlim HostnameLookupGate = new(1, 1);

    public async Task<ScanResult?> ScanAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            ProbeResult probe;

            await DiscoveryGate.WaitAsync(cancellationToken);
            try
            {
                probe = await ProbeAsync(ipAddress, timeoutMs, cancellationToken);

                // Second-chance probe for addresses that looked dead on the first pass.
                // This is the main stabilizer for device counts across repeated scans.
                if (!probe.IsAlive)
                {
                    await Task.Delay(120, cancellationToken);
                    probe = await ProbeAsync(ipAddress, timeoutMs, cancellationToken);
                }
            }
            finally
            {
                DiscoveryGate.Release();
            }

            if (!probe.IsAlive)
            {
                return null;
            }

            var hostname = await ResolveLegacyHostnameAsync(ipAddress, cancellationToken);
            var macAddress = GetMacAddressFromArp(ipAddress);

            return new ScanResult
            {
                IPAddress = ipAddress,
                Hostname = hostname,
                MacAddress = macAddress,
                Manufacturer = OuiLookupService.Lookup(macAddress),
                Status = GetStatus(probe.PingSuccess, probe.Port80Open, probe.Port443Open),
                Port80Open = probe.Port80Open,
                Port443Open = probe.Port443Open
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ProbeResult> ProbeAsync(
        string ipAddress,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var webTimeout = Math.Min(Math.Max(timeoutMs, 300), 900);

        var pingTask = PingAsync(ipAddress, timeoutMs, cancellationToken);
        var port80Task = IsPortOpenAsync(ipAddress, 80, webTimeout, cancellationToken);
        var port443Task = IsPortOpenAsync(ipAddress, 443, webTimeout, cancellationToken);

        await Task.WhenAll(pingTask, port80Task, port443Task);
        cancellationToken.ThrowIfCancellationRequested();

        return new ProbeResult(
            await pingTask,
            await port80Task,
            await port443Task);
    }

    private static string GetStatus(bool pingSuccess, bool port80Open, bool port443Open)
    {
        if (port80Open && port443Open)
        {
            return "HTTP/HTTPS";
        }

        if (port443Open)
        {
            return "HTTPS";
        }

        if (port80Open)
        {
            return "HTTP";
        }

        return pingSuccess ? "Online" : "Unknown";
    }

    private static async Task<bool> PingAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, timeoutMs);
            cancellationToken.ThrowIfCancellationRequested();
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsPortOpenAsync(
        string ipAddress,
        int port,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(ipAddress, port);
            var timeoutTask = Task.Delay(timeoutMs, cancellationToken);

            var completed = await Task.WhenAny(connectTask, timeoutTask);
            if (completed != connectTask)
            {
                return false;
            }

            await connectTask;
            return tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> ResolveLegacyHostnameAsync(
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var gateEntered = false;

        try
        {
            await HostnameLookupGate.WaitAsync(cancellationToken);
            gateEntered = true;

            // Give Windows/ARP state a moment to settle, then use the simple reverse-DNS
            // lookup style from the older scanner rather than the newer DNS/NetBIOS race.
            await Task.Delay(200, cancellationToken);

            var lookupTask = Dns.GetHostEntryAsync(ipAddress);
            var host = await lookupTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            if (!string.IsNullOrWhiteSpace(host.HostName))
            {
                var value = host.HostName.Trim();
                if (!string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
        }
        catch
        {
            // Hostname lookup is best-effort only.
        }
        finally
        {
            if (gateEntered)
            {
                HostnameLookupGate.Release();
            }
        }

        return "Unknown";
    }

    private static string GetMacAddressFromArp(string ipAddress)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "arp",
                Arguments = "-a " + ipAddress,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return "Unknown";
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(500))
            {
                try { process.Kill(); } catch { }
                return "Unknown";
            }

            foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = ArpRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                if (string.Equals(match.Groups["ip"].Value, ipAddress, StringComparison.OrdinalIgnoreCase))
                {
                    return match.Groups["mac"].Value;
                }
            }
        }
        catch
        {
            // MAC lookup is best-effort only.
        }

        return "Unknown";
    }

    private readonly record struct ProbeResult(
        bool PingSuccess,
        bool Port80Open,
        bool Port443Open)
    {
        public bool IsAlive => PingSuccess || Port80Open || Port443Open;
    }
}
