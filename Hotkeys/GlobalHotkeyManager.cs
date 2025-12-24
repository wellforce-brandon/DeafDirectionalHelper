using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeafDirectionalHelper.Hotkeys
{
    public class GlobalHotkeyManager : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifier key constants
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // Virtual key codes
        private const uint VK_R = 0x52; // R key
        private const uint VK_M = 0x4D; // M key
        private const uint VK_S = 0x53; // S key
        private const uint VK_P = 0x50; // P key
        private const uint VK_H = 0x48; // H key

        private IntPtr _windowHandle;
        private HwndSource? _source;
        private bool _isRegistered;

        // Hotkey IDs
        private const int HOTKEY_TOGGLE_ENABLED = 1;
        private const int HOTKEY_TOGGLE_MODE = 2;
        private const int HOTKEY_SHOW_SETTINGS = 3;
        private const int HOTKEY_RESET_POSITIONS = 4;
        private const int HOTKEY_SHOW_HOTKEYS = 5;

        public event Action? ToggleEnabledPressed;
        public event Action? ToggleModePressed;
        public event Action? ShowSettingsPressed;
        public event Action? ResetPositionsPressed;
        public event Action? ShowHotkeysPressed;

        public void Initialize(Window window)
        {
            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.EnsureHandle();

            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(HwndHook);
        }

        public bool RegisterHotkeys()
        {
            if (_windowHandle == IntPtr.Zero) return false;

            // Ctrl+Shift+R - Toggle Enabled
            var success1 = RegisterHotKey(_windowHandle, HOTKEY_TOGGLE_ENABLED, MOD_CONTROL | MOD_SHIFT, VK_R);

            // Ctrl+Shift+M - Toggle Mode
            var success2 = RegisterHotKey(_windowHandle, HOTKEY_TOGGLE_MODE, MOD_CONTROL | MOD_SHIFT, VK_M);

            // Ctrl+Shift+S - Show Settings
            var success3 = RegisterHotKey(_windowHandle, HOTKEY_SHOW_SETTINGS, MOD_CONTROL | MOD_SHIFT, VK_S);

            // Ctrl+Shift+P - Reset Positions
            var success4 = RegisterHotKey(_windowHandle, HOTKEY_RESET_POSITIONS, MOD_CONTROL | MOD_SHIFT, VK_P);

            // Ctrl+Shift+H - Show Hotkeys
            var success5 = RegisterHotKey(_windowHandle, HOTKEY_SHOW_HOTKEYS, MOD_CONTROL | MOD_SHIFT, VK_H);

            _isRegistered = success1 || success2 || success3 || success4 || success5;

            if (!success1) Console.WriteLine("Failed to register Ctrl+Shift+R hotkey");
            if (!success2) Console.WriteLine("Failed to register Ctrl+Shift+M hotkey");
            if (!success3) Console.WriteLine("Failed to register Ctrl+Shift+S hotkey");
            if (!success4) Console.WriteLine("Failed to register Ctrl+Shift+P hotkey");
            if (!success5) Console.WriteLine("Failed to register Ctrl+Shift+H hotkey");

            return _isRegistered;
        }

        public void UnregisterHotkeys()
        {
            if (_windowHandle == IntPtr.Zero) return;

            UnregisterHotKey(_windowHandle, HOTKEY_TOGGLE_ENABLED);
            UnregisterHotKey(_windowHandle, HOTKEY_TOGGLE_MODE);
            UnregisterHotKey(_windowHandle, HOTKEY_SHOW_SETTINGS);
            UnregisterHotKey(_windowHandle, HOTKEY_RESET_POSITIONS);
            UnregisterHotKey(_windowHandle, HOTKEY_SHOW_HOTKEYS);

            _isRegistered = false;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                var hotkeyId = wParam.ToInt32();
                switch (hotkeyId)
                {
                    case HOTKEY_TOGGLE_ENABLED:
                        ToggleEnabledPressed?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_TOGGLE_MODE:
                        ToggleModePressed?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_SHOW_SETTINGS:
                        ShowSettingsPressed?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_RESET_POSITIONS:
                        ResetPositionsPressed?.Invoke();
                        handled = true;
                        break;
                    case HOTKEY_SHOW_HOTKEYS:
                        ShowHotkeysPressed?.Invoke();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterHotkeys();
            _source?.RemoveHook(HwndHook);
        }
    }
}
