namespace N8sIPScanner;

public sealed class NetworkInterfaceInfo
{
    public string InterfaceName { get; init; } = "";
    public string AdapterName { get; init; } = "";
    public string IPv4Address { get; init; } = "";
    public string SubnetMask { get; init; } = "";
    public string AddressMethod { get; init; } = "";
    public string Gateway { get; init; } = "";
    public string PrimaryDns { get; init; } = "";
    public string SecondaryDns { get; init; } = "";
    public string MacAddress { get; init; } = "";
    public string SubnetPrefix { get; init; } = "";
    public string OperationalStatus { get; init; } = "";
    public string InterfaceType { get; init; } = "";
    public bool HasIPv4 { get; init; }
    public bool IsApipa { get; init; }

    public override string ToString()
    {
        var ipText = string.IsNullOrWhiteSpace(IPv4Address) ? "No IPv4" : IPv4Address;
        return $"{InterfaceName}  |  {ipText}  |  {AddressMethod}  |  {OperationalStatus}";
    }
}
