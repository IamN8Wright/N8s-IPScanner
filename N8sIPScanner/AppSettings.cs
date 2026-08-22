namespace N8sIPScanner;

public sealed class AppSettings
{
    public string ThemeMode { get; set; } = "Light";
    public bool ShowLoopbackAdapters { get; set; } = false;
    public bool ShowDisconnectedAdapters { get; set; } = false;
}
