using System;

namespace N8sIPScanner;

public sealed class PassiveDiscoveryObservation
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string SourceIp { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string SuggestedCidr { get; init; } = "";
    public string SuggestedIp { get; init; } = "";
    public string SuggestedMask { get; init; } = "255.255.255.0";
    public string Details { get; init; } = "";
}
