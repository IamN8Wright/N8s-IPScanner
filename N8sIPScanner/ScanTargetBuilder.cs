using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace N8sIPScanner;

public static class ScanTargetBuilder
{
    private const int MaximumScanTargets = 65534;

    public static bool TryBuildFullSubnetTargets(
        string input,
        out List<string> targets,
        out string description,
        out string error)
    {
        targets = new List<string>();
        description = "";
        error = "";

        if (!TryParseCidrInput(input, out var ipValue, out var prefixLength, out error))
        {
            return false;
        }

        var hostCount = GetUsableHostCount(prefixLength);
        if (hostCount <= 0)
        {
            error = "The selected subnet does not contain any usable IPv4 addresses.";
            return false;
        }

        if (hostCount > MaximumScanTargets)
        {
            error =
                $"This subnet contains {hostCount:N0} usable addresses.\n\n" +
                $"For safety, this app currently limits full-subnet scans to {MaximumScanTargets:N0} addresses.\n" +
                "Use a smaller CIDR range, such as /24, /23, /22, /21, or /20.";
            return false;
        }

        var mask = PrefixToMask(prefixLength);
        var network = ipValue & mask;
        var broadcast = network | ~mask;

        uint first;
        uint last;

        if (prefixLength == 32)
        {
            first = ipValue;
            last = ipValue;
        }
        else if (prefixLength == 31)
        {
            first = network;
            last = broadcast;
        }
        else
        {
            first = network + 1;
            last = broadcast - 1;
        }

        for (var value = first; value <= last; value++)
        {
            targets.Add(UIntToIPv4(value));

            if (value == uint.MaxValue)
            {
                break;
            }
        }

        description = $"{UIntToIPv4(network)}/{prefixLength}";
        return true;
    }

    public static bool TryBuildLegacyRangeTargets(
        string input,
        int start,
        int end,
        out List<string> targets,
        out string description,
        out string error)
    {
        targets = new List<string>();
        description = "";
        error = "";

        if (start < 0 || end > 255 || start > end)
        {
            error = "Start and End must be between 0 and 255, and Start must be lower than End.";
            return false;
        }

        if (!TryGetSubnetPrefix(input, out var subnetPrefix))
        {
            error =
                "Enter a valid subnet prefix such as 192.168.1, or a CIDR such as 192.168.1.0/24.\n\n" +
                "For non-/24 networks, check Full subnet and use CIDR notation, such as 10.16.98.0/23.";
            return false;
        }

        for (var host = start; host <= end; host++)
        {
            targets.Add($"{subnetPrefix}.{host}");
        }

        description = $"{subnetPrefix}.{start}-{end}";
        return true;
    }

    public static bool TryNormalizeToCidr(string input, out string normalized)
    {
        normalized = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        input = input.Trim();

        if (input.Contains('/'))
        {
            normalized = input;
            return true;
        }

        if (TryGetSubnetPrefix(input, out var prefix))
        {
            normalized = $"{prefix}.0/24";
            return true;
        }

        if (TryParseIPv4(input, out _))
        {
            normalized = $"{input}/32";
            return true;
        }

        return false;
    }

    public static string GetCidrFromAddressAndMask(string ipAddress, string subnetMask)
    {
        if (!TryParseIPv4(ipAddress, out var ipValue))
        {
            return "";
        }

        if (!TryParseIPv4(subnetMask, out var maskValue))
        {
            return "";
        }

        if (!TryMaskToPrefix(maskValue, out var prefixLength))
        {
            return "";
        }

        var network = ipValue & maskValue;
        return $"{UIntToIPv4(network)}/{prefixLength}";
    }

    private static bool TryParseCidrInput(string input, out uint ipValue, out int prefixLength, out string error)
    {
        ipValue = 0;
        prefixLength = 24;
        error = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Enter a network in CIDR notation, such as 192.168.1.0/24 or 10.16.98.0/23.";
            return false;
        }

        input = input.Trim();

        if (!input.Contains('/'))
        {
            if (TryGetSubnetPrefix(input, out var legacyPrefix))
            {
                input = $"{legacyPrefix}.0/24";
            }
            else if (TryParseIPv4(input, out _))
            {
                input = $"{input}/32";
            }
            else
            {
                error = "Enter a valid CIDR range, such as 192.168.1.0/24 or 10.16.98.0/23.";
                return false;
            }
        }

        var parts = input.Split('/');
        if (parts.Length != 2)
        {
            error = "CIDR notation must look like 192.168.1.0/24.";
            return false;
        }

        if (!TryParseIPv4(parts[0].Trim(), out ipValue))
        {
            error = "The network/IP portion must be a valid IPv4 address.";
            return false;
        }

        if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out prefixLength) ||
            prefixLength < 0 ||
            prefixLength > 32)
        {
            error = "CIDR prefix must be between 0 and 32.";
            return false;
        }

        return true;
    }

    private static bool TryGetSubnetPrefix(string input, out string prefix)
    {
        prefix = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        input = input.Trim();

        if (input.Contains('/'))
        {
            var firstPart = input.Split('/')[0].Trim();
            if (!TryParseIPv4(firstPart, out _))
            {
                return false;
            }

            var partsFromCidr = firstPart.Split('.');
            prefix = $"{partsFromCidr[0]}.{partsFromCidr[1]}.{partsFromCidr[2]}";
            return true;
        }

        var parts = input.Split('.');
        if (parts.Length == 3)
        {
            if (parts.All(p => int.TryParse(p, out var n) && n is >= 0 and <= 255))
            {
                prefix = $"{parts[0]}.{parts[1]}.{parts[2]}";
                return true;
            }
        }

        if (parts.Length == 4 && TryParseIPv4(input, out _))
        {
            prefix = $"{parts[0]}.{parts[1]}.{parts[2]}";
            return true;
        }

        return false;
    }

    private static int GetUsableHostCount(int prefixLength)
    {
        if (prefixLength == 32)
        {
            return 1;
        }

        if (prefixLength == 31)
        {
            return 2;
        }

        if (prefixLength < 16)
        {
            return MaximumScanTargets + 1;
        }

        var total = 1 << (32 - prefixLength);
        return Math.Max(0, total - 2);
    }

    private static bool TryMaskToPrefix(uint mask, out int prefixLength)
    {
        prefixLength = 0;
        var zeroSeen = false;

        for (var bit = 31; bit >= 0; bit--)
        {
            var isOne = ((mask >> bit) & 1) == 1;

            if (isOne)
            {
                if (zeroSeen)
                {
                    return false;
                }

                prefixLength++;
            }
            else
            {
                zeroSeen = true;
            }
        }

        return true;
    }

    private static uint PrefixToMask(int prefixLength)
    {
        if (prefixLength == 0)
        {
            return 0;
        }

        return uint.MaxValue << (32 - prefixLength);
    }

    private static bool TryParseIPv4(string value, out uint result)
    {
        result = 0;

        if (!IPAddress.TryParse(value, out var parsed) ||
            parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = parsed.GetAddressBytes();
        result =
            ((uint)bytes[0] << 24) |
            ((uint)bytes[1] << 16) |
            ((uint)bytes[2] << 8) |
            bytes[3];

        return true;
    }

    private static string UIntToIPv4(uint value)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(value >> 24) & 255}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}");
    }
}
