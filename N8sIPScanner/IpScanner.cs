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

    public async Task<ScanResult?> ScanAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            var webTimeout = Math.Min(Math.Max(timeoutMs, 100), 450);

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

            var hostname = await ResolveHostnameAsync(ipAddress, 350, cancellationToken);
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

    private static async Task<string> ResolveHostnameAsync(string ipAddress, int timeoutMs, CancellationToken cancellationToken)
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
            return string.IsNullOrWhiteSpace(host.HostName) ? "Unknown" : host.HostName;
        }
        catch
        {
            return "Unknown";
        }
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
