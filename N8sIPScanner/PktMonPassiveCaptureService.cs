using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace N8sIPScanner;

public static class PktMonPassiveCaptureService
{
    private static readonly Regex IPv4Regex = new(
        @"\b(?<ip>(?:\d{1,3}\.){3}\d{1,3})\b",
        RegexOptions.Compiled);

    public static async Task<IReadOnlyList<PassiveDiscoveryObservation>> CaptureAsync(int seconds)
    {
        return await Task.Run(() =>
        {
            var tempBase = Path.Combine(Path.GetTempPath(), "N8s-IPScanner-PassiveCapture");
            Directory.CreateDirectory(tempBase);

            var id = Guid.NewGuid().ToString("N");
            var etlPath = Path.Combine(tempBase, $"capture-{id}.etl");
            var txtPath = Path.Combine(tempBase, $"capture-{id}.txt");
            var logPath = Path.Combine(tempBase, $"capture-{id}.log");
            var batchPath = Path.Combine(tempBase, $"capture-{id}.cmd");

            try
            {
                File.WriteAllText(batchPath, BuildBatch(seconds, etlPath, txtPath, logPath), Encoding.ASCII);

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + Quote(batchPath),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                if (!IsRunningAsAdministrator())
                {
                    psi.Verb = "runas";
                }

                using var process = Process.Start(psi);
                if (process is null)
                {
                    throw new InvalidOperationException("Windows did not start the packet capture command.");
                }

                process.WaitForExit();

                var log = File.Exists(logPath) ? File.ReadAllText(logPath, Encoding.UTF8) : "";
                var formatted = File.Exists(txtPath) ? File.ReadAllText(txtPath, Encoding.UTF8) : "";

                if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(formatted))
                {
                    throw new InvalidOperationException(
                        "Windows Packet Monitor did not complete successfully.\n\n" +
                        "Details:\n" +
                        log);
                }

                var combined = formatted + "\n" + log;
                var observations = ParseObservations(combined);

                return (IReadOnlyList<PassiveDiscoveryObservation>)observations;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new OperationCanceledException("The administrator prompt was canceled.");
            }
            finally
            {
                TryDelete(batchPath);
                TryDelete(etlPath);
                TryDelete(txtPath);
                TryDelete(logPath);
            }
        });
    }

    private static string BuildBatch(int seconds, string etlPath, string txtPath, string logPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("setlocal");
        sb.AppendLine($"echo N8's IP Scanner advanced passive capture> {Quote(logPath)}");
        sb.AppendLine($"echo Started: %DATE% %TIME%>> {Quote(logPath)}");
        sb.AppendLine($"where pktmon >> {Quote(logPath)} 2>&1");
        sb.AppendLine("if errorlevel 1 exit /b 9009");
        sb.AppendLine($"pktmon stop >> {Quote(logPath)} 2>&1");
        sb.AppendLine($"pktmon filter remove >> {Quote(logPath)} 2>&1");
        sb.AppendLine($"pktmon start --capture --pkt-size 256 --comp nics --file-name {Quote(etlPath)} >> {Quote(logPath)} 2>&1");
        sb.AppendLine("if errorlevel 1 (");
        sb.AppendLine($"  echo First pktmon start command failed, trying fallback.>> {Quote(logPath)}");
        sb.AppendLine($"  pktmon start --capture --pkt-size 256 --file-name {Quote(etlPath)} >> {Quote(logPath)} 2>&1");
        sb.AppendLine(")");
        sb.AppendLine("if errorlevel 1 exit /b %ERRORLEVEL%");
        sb.AppendLine($"timeout /t {Math.Clamp(seconds, 5, 600)} /nobreak >nul");
        sb.AppendLine($"pktmon stop >> {Quote(logPath)} 2>&1");
        sb.AppendLine($"pktmon format {Quote(etlPath)} -o {Quote(txtPath)} >> {Quote(logPath)} 2>&1");
        sb.AppendLine("if errorlevel 1 (");
        sb.AppendLine($"  echo pktmon format failed. This Windows build may use a different pktmon output format.>> {Quote(logPath)}");
        sb.AppendLine(")");
        sb.AppendLine("exit /b 0");
        return sb.ToString();
    }

    private static List<PassiveDiscoveryObservation> ParseObservations(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<PassiveDiscoveryObservation>();

        foreach (Match match in IPv4Regex.Matches(text))
        {
            var raw = match.Groups["ip"].Value;

            if (!IPAddress.TryParse(raw, out var ip) ||
                ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                continue;
            }

            if (!IsUsefulAddress(ip))
            {
                continue;
            }

            var octets = ip.GetAddressBytes();
            var cidr = $"{octets[0]}.{octets[1]}.{octets[2]}.0/24";
            var key = $"{raw}|{cidr}";

            if (!seen.Add(key))
            {
                continue;
            }

            var suggestedHost = PickSuggestedHost(octets[3]);

            results.Add(new PassiveDiscoveryObservation
            {
                Timestamp = DateTimeOffset.Now,
                SourceIp = raw,
                Protocol = "PktMon",
                SuggestedCidr = cidr,
                SuggestedIp = $"{octets[0]}.{octets[1]}.{octets[2]}.{suggestedHost}",
                SuggestedMask = "255.255.255.0",
                Details = "Windows Packet Monitor heard this IPv4 address. Mask cannot be proven passively; /24 is a starting guess."
            });
        }

        return results
            .OrderBy(r => r.SuggestedCidr, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.SourceIp, StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();
    }

    private static bool IsUsefulAddress(IPAddress address)
    {
        var b = address.GetAddressBytes();

        if (b[0] == 0 || b[0] == 127)
        {
            return false;
        }

        if (b[0] >= 224)
        {
            return false;
        }

        if (b[0] == 169 && b[1] == 254)
        {
            return false;
        }

        if (b[0] == 255 && b[1] == 255 && b[2] == 255 && b[3] == 255)
        {
            return false;
        }

        return true;
    }

    private static int PickSuggestedHost(byte observedHost)
    {
        foreach (var host in new[] { 250, 251, 249, 248, 247, 246, 245 })
        {
            if (host != observedHost)
            {
                return host;
            }
        }

        return 250;
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary cleanup is best-effort.
        }
    }
}
