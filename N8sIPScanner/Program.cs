using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace N8sIPScanner;

internal static class Program
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    [STAThread]
    private static void Main()
    {
        _ = SetCurrentProcessExplicitAppUserModelID("N8Tools.N8s IP Scanner");
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new MainForm());
    }
}
