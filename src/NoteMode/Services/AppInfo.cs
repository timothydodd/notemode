using System;
using System.Runtime.InteropServices;

namespace NoteMode.Services;

public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/timothydodd/notemode";

    /// <summary>The running build's version (major.minor.patch), stamped from the release tag in CI.</summary>
    public static string Version
    {
        get
        {
            var v = typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
        }
    }

    /// <summary>
    /// True when running from the MSIX (Microsoft Store) package. Such an app cannot register file
    /// associations in the registry; the package manifest declares them instead.
    /// </summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(8))
            return false;
        try
        {
            var length = 0;
            // APPMODEL_ERROR_NO_PACKAGE (15700) when unpackaged; ERROR_INSUFFICIENT_BUFFER (122) when packaged.
            return GetCurrentPackageFullName(ref length, IntPtr.Zero) != 15700;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, IntPtr packageFullName);
}
