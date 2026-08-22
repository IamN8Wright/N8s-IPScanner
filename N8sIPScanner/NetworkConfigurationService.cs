using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace N8sIPScanner;

public static class NetworkConfigurationService
{
    public static async Task ApplyStaticAsync(
        string interfaceName,
        string ipAddress,
        string subnetMask,
        string defaultGateway,
        string primaryDns,
        string secondaryDns)
    {
        ValidateInterfaceName(interfaceName);
        ValidateIPv4(ipAddress, "IP address");
        ValidateIPv4(subnetMask, "Subnet mask");

        if (!string.IsNullOrWhiteSpace(defaultGateway))
        {
            ValidateIPv4(defaultGateway, "Default gateway");
        }

        if (!string.IsNullOrWhiteSpace(primaryDns))
        {
            ValidateIPv4(primaryDns, "Primary DNS");
        }

        if (!string.IsNullOrWhiteSpace(secondaryDns))
        {
            ValidateIPv4(secondaryDns, "Secondary DNS");

            if (string.IsNullOrWhiteSpace(primaryDns))
            {
                throw new ArgumentException("Primary DNS is required when Secondary DNS is entered.");
            }
        }

        var batch = new StringBuilder();

        // Use named netsh arguments. They are less brittle than positional arguments.
        if (string.IsNullOrWhiteSpace(defaultGateway))
        {
            batch.AppendLine(
                $"netsh interface ipv4 set address name={Quote(interfaceName)} source=static address={ipAddress} mask={subnetMask} gateway=none");
        }
        else
        {
            batch.AppendLine(
                $"netsh interface ipv4 set address name={Quote(interfaceName)} source=static address={ipAddress} mask={subnetMask} gateway={defaultGateway} gwmetric=1");
        }

        if (!string.IsNullOrWhiteSpace(primaryDns))
        {
            batch.AppendLine(
                $"netsh interface ipv4 set dnsservers name={Quote(interfaceName)} source=static address={primaryDns} register=primary validate=no");

            if (!string.IsNullOrWhiteSpace(secondaryDns))
            {
                batch.AppendLine(
                    $"netsh interface ipv4 add dnsservers name={Quote(interfaceName)} address={secondaryDns} index=2 validate=no");
            }
        }

        await RunElevatedBatchAsync(batch.ToString());
    }

    public static async Task SetDhcpAsync(string interfaceName)
    {
        ValidateInterfaceName(interfaceName);

        var batch = new StringBuilder();
        batch.AppendLine($"netsh interface ipv4 set address name={Quote(interfaceName)} source=dhcp");
        batch.AppendLine($"netsh interface ipv4 set dnsservers name={Quote(interfaceName)} source=dhcp");

        await RunElevatedBatchAsync(batch.ToString());
    }

    private static async Task RunElevatedBatchAsync(string commands)
    {
        await Task.Run(() =>
        {
            var tempBase = Path.Combine(Path.GetTempPath(), "N8s IP Scanner");
            Directory.CreateDirectory(tempBase);

            var id = Guid.NewGuid().ToString("N");
            var batchPath = Path.Combine(tempBase, $"netcfg-{id}.cmd");
            var logPath = Path.Combine(tempBase, $"netcfg-{id}.log");

            try
            {
                var batch = new StringBuilder();
                batch.AppendLine("@echo off");
                batch.AppendLine("setlocal");
                batch.AppendLine($"echo N8s IP Scanner network settings log> {CmdQuote(logPath)}");
                batch.AppendLine($"echo Started: %DATE% %TIME%>> {CmdQuote(logPath)}");
                batch.AppendLine($"echo.>> {CmdQuote(logPath)}");

                foreach (var rawLine in commands.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    batch.AppendLine($"echo COMMAND: {EscapeForEcho(line)}>> {CmdQuote(logPath)}");
                    batch.AppendLine($"{line} >> {CmdQuote(logPath)} 2>&1");
                    batch.AppendLine("if errorlevel 1 (");
                    batch.AppendLine($"  echo FAILED WITH ERRORLEVEL %ERRORLEVEL%>> {CmdQuote(logPath)}");
                    batch.AppendLine("  exit /b %ERRORLEVEL%");
                    batch.AppendLine(")");
                    batch.AppendLine($"echo OK>> {CmdQuote(logPath)}");
                    batch.AppendLine($"echo.>> {CmdQuote(logPath)}");
                }

                batch.AppendLine("exit /b 0");
                File.WriteAllText(batchPath, batch.ToString(), Encoding.ASCII);

                var isElevated = IsRunningAsAdministrator();

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + CmdQuote(batchPath),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                // Default behavior: app opens normally. UAC is requested only for NIC changes.
                // If the app is already elevated, this avoids a second prompt.
                if (!isElevated)
                {
                    psi.Verb = "runas";
                }

                using var process = Process.Start(psi);
                if (process is null)
                {
                    throw new InvalidOperationException("Windows did not start the networking command.");
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var details = File.Exists(logPath)
                        ? File.ReadAllText(logPath)
                        : "No netsh log was created.";

                    throw new InvalidOperationException(
                        "The Windows networking command failed.\n\n" +
                        "Details:\n" +
                        details);
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new OperationCanceledException("The administrator prompt was canceled.");
            }
            finally
            {
                TryDelete(batchPath);

                // Keep the log only if there was a failure. Successful logs are cleaned up.
                // The catch path reads before finally runs, so this file may be deleted after the message is built.
                // If debugging is needed, comment out this line.
                TryDelete(logPath);
            }
        });
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void ValidateInterfaceName(string interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new ArgumentException("No network interface is selected.");
        }

        if (interfaceName.Contains('"'))
        {
            throw new ArgumentException("The selected network interface name contains an unsupported quote character.");
        }
    }

    private static void ValidateIPv4(string value, string label)
    {
        if (!IPAddress.TryParse(value.Trim(), out var parsed) ||
            parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException($"{label} must be a valid IPv4 address.");
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string CmdQuote(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeForEcho(string value)
    {
        return value
            .Replace("^", "^^")
            .Replace("&", "^&")
            .Replace("|", "^|")
            .Replace("<", "^<")
            .Replace(">", "^>");
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
            // Not important enough to bother the user.
        }
    }
}
