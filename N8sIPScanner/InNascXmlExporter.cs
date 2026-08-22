using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace N8sIPScanner;

public static class InNascXmlExporter
{
    public static XDocument Build(IEnumerable<ScanResult> results, string scanDescription)
    {
        var createdUtc = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        var root = new XElement(
            "InNascImport",
            new XAttribute("schemaVersion", "1"),
            new XAttribute("source", "N8s IP Scanner"),
            new XAttribute("createdUtc", createdUtc),
            new XAttribute("scanDescription", scanDescription));

        var equipment = new XElement("Equipment");
        root.Add(equipment);

        foreach (var result in results.OrderBy(r => IPv4SortKey(r.IPAddress)))
        {
            var displayName = ChooseDescription(result);

            var device = new XElement(
                "Device",
                new XAttribute("sourceId", StableSourceId(result)),
                new XElement("Description", displayName),
                new XElement("Manufacturer", Clean(result.Manufacturer)),
                new XElement("Hostname", Clean(result.Hostname)),
                new XElement("Serial", ""),
                new XElement("Firmware", ""),
                new XElement("PrimaryIP", Clean(result.IPAddress)),
                new XElement("SecondaryIPs", ""),
                new XElement("MACs", Clean(result.MacAddress)),
                new XElement("Subnet", ""),
                new XElement("Gateway", ""),
                new XElement("Username", ""),
                new XElement("Password", ""),
                new XElement("Notes", BuildNotes(result)),
                new XElement(
                    "NetworkInterfaces",
                    new XElement(
                        "Interface",
                        new XAttribute("type", "Primary"),
                        new XElement("IPAddress", Clean(result.IPAddress)),
                        new XElement("MACAddress", Clean(result.MacAddress)),
                        new XElement("Manufacturer", Clean(result.Manufacturer)),
                        new XElement("Status", Clean(result.Status)),
                        new XElement("HasWebUi", result.HasWebUi ? "true" : "false"),
                        new XElement("WebUrl", result.HasWebUi ? result.PreferredUrl : ""))));

            equipment.Add(device);
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root);
    }

    private static string ChooseDescription(ScanResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Hostname) &&
            !string.Equals(result.Hostname, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return Clean(result.Hostname);
        }

        if (!string.IsNullOrWhiteSpace(result.Manufacturer) &&
            !string.Equals(result.Manufacturer, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return $"{Clean(result.Manufacturer)} device {Clean(result.IPAddress)}";
        }

        return $"Discovered device {Clean(result.IPAddress)}";
    }

    private static string BuildNotes(ScanResult result)
    {
        var parts = new List<string>
        {
            "Discovered by N8sIPScanner.",
            $"Status: {Clean(result.Status)}."
        };

        if (result.HasWebUi)
        {
            parts.Add($"Web UI: {result.PreferredUrl}");
        }

        return string.Join(" ", parts);
    }

    private static string StableSourceId(ScanResult result)
    {
        var mac = Clean(result.MacAddress);
        if (!string.IsNullOrWhiteSpace(mac) &&
            !string.Equals(mac, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "mac:" + mac.Replace("-", "").Replace(":", "").ToUpperInvariant();
        }

        return "ip:" + Clean(result.IPAddress);
    }

    private static string Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }

    private static long IPv4SortKey(string ipAddress)
    {
        var parts = ipAddress.Split('.');
        if (parts.Length != 4)
        {
            return long.MaxValue;
        }

        long value = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var octet) ||
                octet < 0 ||
                octet > 255)
            {
                return long.MaxValue;
            }

            value = (value << 8) + octet;
        }

        return value;
    }
}
