using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeafDirectionalHelper.Helpers;

/// <summary>
/// Applies the dark (immersive) title bar to a window via DWM.
/// Call from any window constructor; the attribute is set once the
/// native handle exists (SourceInitialized).
/// </summary>
public static class DarkChrome
{
    // Win10 20H1+ value; pre-20H1 builds used 19.
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetDarkMode(hwnd);
            return;
        }

        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
                SetDarkMode(handle);
        };
    }

    private static void SetDarkMode(IntPtr hwnd)
    {
        int enabled = 1;
        try
        {
            var result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
            if (result != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1, ref enabled, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // No DWM available; keep the default title bar.
        }
    }
}
