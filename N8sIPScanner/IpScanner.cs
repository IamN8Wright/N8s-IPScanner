using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace N8sIPScanner;

public sealed class IpScanner
{
    private static readonly Regex ArpRegex = new(
        @"^\s*(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9a-fA-F-]{17})\s+",
        RegexOptions.Compiled);

    private static readonly Regex NetBiosNameRegex = new(
        @"^\s*(?<name>[^\s<]{1,15})\s+<00>\s+UNIQUE\s+Registered",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Discovery can remain moderately parallel, but Windows name resolution is much
    // more reliable when we do not launch dozens of DNS/NetBIOS lookups at once.
    private static readonly SemaphoreSlim HostnameLookupGate = new(6, 6);

    public async Task<ScanResult?> ScanAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            // Balanced discovery: still responsive on empty addresses, while allowing
            // slower web-capable AV endpoints a reasonable window to answer.
            var webTimeout = Math.Min(Math.Max(timeoutMs, 150), 600);

            var pingTask = PingAsync(ipAddress, timeoutMs, cancellationToken);
            var port80Task = IsPortOpenAsync(ipAddress, 80, webTimeout, cancellationToken);
            var port443Task = IsPortOpenAsync(ipAddress, 443, webTimeout, cancellationToken);

            await Task.WhenAll(pingTask, port80Task, port443Task);
            cancellationToken.ThrowIfCancellationRequested();

            var pingSuccess = await pingTask;
            var port80Open = await port80Task;
            var port443Open = await port443Task;

            if (!pingSuccess && !port80Open && !port443Open)
            {
                return null;
            }

            var hostname = await ResolveBestHostnameAsync(ipAddress, cancellationToken);
            var macAddress = GetMacAddressFromArp(ipAddress);

            return new ScanResult
            {
                IPAddress = ipAddress,
                Hostname = hostname,
                MacAddress = macAddress,
                Manufacturer = OuiLookupService.Lookup(macAddress),
                Status = GetStatus(pingSuccess, port80Open, port443Open),
                Port80Open = port80Open,
                Port443Open = port443Open
            };
        }
        catch
        {
            return null;
        }
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

    private static async Task<bool> IsPortOpenAsync(string ipAddress, int port, int timeoutMs, CancellationToken cancellationToken)
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

    private static async Task<string> ResolveBestHostnameAsync(string ipAddress, CancellationToken cancellationToken)
    {
        // Hostname-priority balanced profile:
        // - only six devices perform hostname work at once;
        // - live devices get a few seconds for Windows name resolution;
        // - offline addresses never pay this cost because enrichment begins only after discovery.
        const int hostnameBudgetMs = 3200;
        const int dnsTimeoutMs = 2800;
        const int netBiosTimeoutMs = 2400;
        const int settleDelayMs = 80;

        var gateEntered = false;

        try
        {
            await HostnameLookupGate.WaitAsync(cancellationToken);
            gateEntered = true;

            // A tiny pause after discovery gives ARP/name-resolution state time to settle,
            // which helps on slower embedded and AV endpoints without materially slowing a scan.
            await Task.Delay(settleDelayMs, cancellationToken);

            using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budgetCts.CancelAfter(hostnameBudgetMs);

            var lookups = new List<Task<string>>
            {
                ResolveDnsHostnameAsync(ipAddress, dnsTimeoutMs, budgetCts.Token),
                ResolveNetBiosHostnameAsync(ipAddress, netBiosTimeoutMs, budgetCts.Token)
            };

            while (lookups.Count > 0 && !budgetCts.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(lookups);
                lookups.Remove(completed);

                var hostname = await completed;
                if (IsUsefulHostname(hostname))
                {
                    return hostname;
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

    private static async Task<string> ResolveDnsHostnameAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            var lookupTask = Dns.GetHostEntryAsync(ipAddress);
            var timeoutTask = Task.Delay(timeoutMs, cancellationToken);

            var completed = await Task.WhenAny(lookupTask, timeoutTask);
            if (completed != lookupTask)
            {
                return "Unknown";
            }

            var host = await lookupTask;
            return string.IsNullOrWhiteSpace(host.HostName) ? "Unknown" : host.HostName.Trim();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static async Task<string> ResolveNetBiosHostnameAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nbtstat",
                Arguments = "-A " + ipAddress,
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

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(timeoutMs, cancellationToken);

            var completed = await Task.WhenAny(exitTask, timeoutTask);
            if (completed != exitTask)
            {
                try { process.Kill(); } catch { }
                return "Unknown";
            }

            var output = await outputTask;
            foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = NetBiosNameRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var candidate = match.Groups["name"].Value.Trim();
                if (IsUsefulHostname(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // NetBIOS hostname lookup is best-effort only.
        }

        return "Unknown";
    }

    private static bool IsUsefulHostname(string? hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        var value = hostname.Trim();
        return !string.Equals(value, "Unknown", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(value, "WORKGROUP", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(value, "MSHOME", StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith("__", StringComparison.OrdinalIgnoreCase);
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
            if (!process.WaitForExit(350))
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
}
