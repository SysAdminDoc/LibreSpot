using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace LibreSpot.Desktop.Services;

public static class Win11ShellIntegration
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_AUTO = 0;
    private const int DWMSBT_MAINWINDOW = 2;
    // Passing DWMWA_COLOR_DEFAULT removes a caption/border/text color override so the
    // system (including the active high-contrast theme) draws the chrome again.
    private const int DWMWA_COLOR_DEFAULT = unchecked((int)0xFFFFFFFF);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyMicaAndDarkChrome(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        ApplyChrome(window, hwnd);

        // Re-apply (or clear) the custom chrome whenever high contrast toggles at
        // runtime. ThemeManager.Initialize subscribed to the same event earlier in
        // App.OnStartup, so by the time this handler runs the palette dictionaries
        // have already been swapped and resource lookups below see the new palette.
        PropertyChangedEventHandler onSystemParametersChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
            {
                ApplyChrome(window, hwnd);
            }
        };
        SystemParameters.StaticPropertyChanged += onSystemParametersChanged;
        window.Closed += (_, _) => SystemParameters.StaticPropertyChanged -= onSystemParametersChanged;
    }

    private static void ApplyChrome(Window window, IntPtr hwnd)
    {
        if (SystemParameters.HighContrast)
        {
            ClearCustomChrome(window, hwnd);
            return;
        }

        // Attribute 20 works on Win10 1903+ and all Win11. On Win10 1809-1903, the
        // correct attribute is 19 (undocumented). Try 20 first, fall back to 19.
        var useDark = 1;
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY, ref useDark, sizeof(int));
        }

        var captionColor = ToColorRef(0x0B, 0x0F, 0x0D);
        var captionTextColor = ToColorRef(0xEA, 0xF2, 0xED);
        var borderColor = ToColorRef(0x2A, 0x36, 0x30);
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref captionTextColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));

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

    private static void ClearCustomChrome(Window window, IntPtr hwnd)
    {
        // High contrast: the user's chosen HC theme owns the title bar. Remove the
        // dark-mode hint and every custom caption/text/border color override.
        var useDark = 0;
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY, ref useDark, sizeof(int));
        }

        var captionColor = DWMWA_COLOR_DEFAULT;
        var captionTextColor = DWMWA_COLOR_DEFAULT;
        var borderColor = DWMWA_COLOR_DEFAULT;
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref captionTextColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));

        if (Environment.OSVersion.Version.Build >= 22621)
        {
            var backdrop = DWMSBT_AUTO;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }

        // Undo the transparent Mica background: in high contrast the swapped palette's
        // CanvasBrush maps to the system window color, which must render opaquely.
        if (window.TryFindResource("CanvasBrush") is Brush canvasBrush)
        {
            window.Background = canvasBrush;
        }
    }

    private static int ToColorRef(byte red, byte green, byte blue) =>
        red | (green << 8) | (blue << 16);
}
