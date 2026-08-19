using System.Runtime.InteropServices;

namespace DriverManager.Core;

public static class PlatformHelper
{
    public static bool IsAdministrator()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        return false;
    }

    public static bool IsPnpSuccess(int exitCode)
    {
        return exitCode is 0 or 259;
    }
}
