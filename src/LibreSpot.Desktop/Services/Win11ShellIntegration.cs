using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LibreSpot.Desktop.Services;

public static class Win11ShellIntegration
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_MAINWINDOW = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyMicaAndDarkChrome(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();

        // Attribute 20 works on Win10 1903+ and all Win11. On Win10 1809-1903, the
        // correct attribute is 19 (undocumented). Try 20 first, fall back to 19.
        var useDark = 1;
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));
        }

        if (Environment.OSVersion.Version.Build < 22621)
        {
            return;
        }

        var backdrop = DWMSBT_MAINWINDOW;
        if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int)) != 0)
        {
            return;
        }

        // Mica requires the window background to be transparent so the DWM backdrop bleeds through.
        // If the style sets a solid Background, the backdrop is visible only under the (non-)chrome.
        // We don't override the resource-bound Background — instead the user opts in via a
        // MicaCanvasBrush resource that happens to be transparent.
        if (window.TryFindResource("MicaCanvasBrush") is Brush micaBrush)
        {
            window.Background = micaBrush;
        }
    }
}
