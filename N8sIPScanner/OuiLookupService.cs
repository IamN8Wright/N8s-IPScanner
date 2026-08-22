using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace N8sIPScanner;

public static class OuiLookupService
{
    private const string IeeeOuiCsvUrl = "https://standards-oui.ieee.org/oui/oui.csv";

    private static readonly object SyncRoot = new();
    private static Dictionary<string, string>? _cache;

    private static readonly Dictionary<string, string> SeedVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Small built-in starter list. Use Update OUI List for the full IEEE MA-L public listing.
        ["00000C"] = "Cisco Systems, Inc",
        ["00000E"] = "Fujitsu Limited",
        ["0000F8"] = "Digital Equipment Corporation",
        ["0001E6"] = "Hewlett Packard",
        ["00022D"] = "Agere Systems",
        ["0004F2"] = "Polycom",
        ["000A27"] = "Apple, Inc.",
        ["000A95"] = "Apple, Inc.",
        ["0010FA"] = "Apple, Inc.",
        ["001124"] = "Apple, Inc.",
        ["00163E"] = "Xensource, Inc.",
        ["001B63"] = "Apple, Inc.",
        ["001C42"] = "Parallels, Inc.",
        ["0021E9"] = "Apple, Inc.",
        ["002500"] = "Apple, Inc.",
        ["0026BB"] = "Apple, Inc.",
        ["005056"] = "VMware, Inc.",
        ["080020"] = "Oracle / Sun Microsystems",
        ["080027"] = "PCS Systemtechnik GmbH / VirtualBox",
        ["3C5A37"] = "Samsung Electronics",
        ["3C5AB4"] = "Google, Inc.",
        ["525400"] = "QEMU / KVM",
        ["7845C4"] = "Dell Inc.",
        ["8C8590"] = "Apple, Inc.",
        ["A45E60"] = "Apple, Inc.",
        ["B827EB"] = "Raspberry Pi Foundation",
        ["BC92B1"] = "Apple, Inc.",
        ["D850E6"] = "ASUSTek Computer Inc.",
        ["F0D5BF"] = "Intel Corporate",
        ["F4F5D8"] = "Google, Inc."
    };

    public static string Lookup(string macAddress)
    {
        var prefix = NormalizeOui(macAddress);
        if (prefix.Length != 6)
        {
            return "Unknown";
        }

        var db = GetDatabase();
        return db.TryGetValue(prefix, out var vendor) ? vendor : "Unknown";
    }

    public static string GetDatabaseStatus()
    {
        var path = GetUserOuiPath();

        if (File.Exists(path))
        {
            var modified = File.GetLastWriteTime(path);
            var count = GetDatabase().Count;
            return $"OUI database: {count:N0} entries, updated {modified:g}";
        }

        return $"OUI database: built-in starter list only ({SeedVendors.Count:N0} entries)";
    }

    public static async Task<int> UpdateFromIeeeAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("N8s IP Scanner/2.1");

        var csv = await client.GetStringAsync(IeeeOuiCsvUrl, cancellationToken);

        if (string.IsNullOrWhiteSpace(csv) ||
            !csv.Contains("Assignment", StringComparison.OrdinalIgnoreCase) ||
            !csv.Contains("Organization", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded IEEE OUI file did not look like the expected CSV.");
        }

        var parsed = ParseIeeeCsv(csv);
        if (parsed.Count < 1000)
        {
            throw new InvalidOperationException("The downloaded IEEE OUI file contained too few entries to trust.");
        }

        var dir = GetDataDirectory();
        Directory.CreateDirectory(dir);

        File.WriteAllText(GetUserOuiPath(), csv, Encoding.UTF8);

        lock (SyncRoot)
        {
            _cache = null;
        }

        return GetDatabase().Count;
    }

    private static Dictionary<string, string> GetDatabase()
    {
        lock (SyncRoot)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var merged = new Dictionary<string, string>(SeedVendors, StringComparer.OrdinalIgnoreCase);
            var path = GetUserOuiPath();

            if (File.Exists(path))
            {
                try
                {
                    var csv = File.ReadAllText(path, Encoding.UTF8);
                    foreach (var pair in ParseIeeeCsv(csv))
                    {
                        merged[pair.Key] = pair.Value;
                    }
                }
                catch
                {
                    // Keep the seed list if the downloaded file is damaged.
                }
            }

            _cache = merged;
            return _cache;
        }
    }

    private static Dictionary<string, string> ParseIeeeCsv(string csv)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(csv);
        var header = reader.ReadLine();

        if (header is null)
        {
            return result;
        }

        var headers = SplitCsvLine(header);
        var assignmentIndex = FindHeader(headers, "Assignment");
        var organizationIndex = FindHeader(headers, "Organization Name", "Organization");

        if (assignmentIndex < 0 || organizationIndex < 0)
        {
            return result;
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = SplitCsvLine(line);

            if (fields.Count <= Math.Max(assignmentIndex, organizationIndex))
            {
                continue;
            }

            var assignment = CleanHex(fields[assignmentIndex]);
            var organization = fields[organizationIndex].Trim();

            if (assignment.Length >= 6 && !string.IsNullOrWhiteSpace(organization))
            {
                result[assignment[..6]] = organization;
            }
        }

        return result;
    }

    private static int FindHeader(IReadOnlyList<string> headers, params string[] names)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            foreach (var name in names)
            {
                if (string.Equals(headers[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string NormalizeOui(string macAddress)
    {
        var cleaned = CleanHex(macAddress);
        return cleaned.Length >= 6 ? cleaned[..6] : "";
    }

    private static string CleanHex(string value)
    {
        var chars = value
            .Where(Uri.IsHexDigit)
            .Select(char.ToUpperInvariant)
            .ToArray();

        return new string(chars);
    }

    private static string GetDataDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "N8s IP Scanner");
    }

    private static string GetUserOuiPath()
    {
        return Path.Combine(GetDataDirectory(), "oui.csv");
    }
}
