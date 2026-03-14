#pragma warning disable CA1416 // Platform compatibility - this is a Windows-only application
using System.Diagnostics;
using System.Security.Principal;

namespace SGL.JudgeDredd.Shared.Helpers;

public static class AdminHelper
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdmin()
    {
        var exePath = Environment.ProcessPath;
        if (exePath == null) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User declined UAC prompt
        }
    }
}
