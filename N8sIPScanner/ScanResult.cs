namespace N8sIPScanner;

public sealed class ScanResult
{
    public string IPAddress { get; init; } = "";
    public string Hostname { get; init; } = "";
    public string MacAddress { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Status { get; init; } = "";
    public bool Port80Open { get; init; }
    public bool Port443Open { get; init; }

    public bool HasWebUi => Port80Open || Port443Open;

    public string PreferredUrl
    {
        get
        {
            if (Port443Open)
            {
                return $"https://{IPAddress}/";
            }

            if (Port80Open)
            {
                return $"http://{IPAddress}/";
            }

            return "";
        }
    }
}
