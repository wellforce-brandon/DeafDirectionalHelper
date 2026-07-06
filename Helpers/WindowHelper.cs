using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace DeafDirectionalHelper.Helpers;

/// <summary>
/// Helper methods for WPF window behaviors shared across views.
/// </summary>
public static class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    /// <summary>
    /// Sets whether a window is click-through (transparent to mouse input).
    /// </summary>
    public static void SetClickThrough(Window window, bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (clickThrough)
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        else
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
    }

    /// <summary>
    /// Re-asserts a window's topmost z-position without activating it.
    /// WPF's Topmost property can silently lose its Win32 z-band when a
    /// non-activated transparent window is followed by another window being
    /// shown and activated (the overlay-under-the-settings-window bug).
    /// </summary>
    public static void ReassertTopmost(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Animates a window's opacity with easing.
    /// </summary>
    public static void AnimateOpacity(Window window, double targetOpacity, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        window.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
