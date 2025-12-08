using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SubtitleReader.Services;

public class HotkeyService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    // Модификаторы
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private IntPtr _windowHandle;
    private HwndSource? _source;
    private int _currentId = 0;
    private bool _isDisposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);
    }

    /// <summary>
    /// Регистрирует горячую клавишу
    /// </summary>
    public int RegisterHotkey(uint modifiers, uint key, Action action)
    {
        int id = ++_currentId;

        if (RegisterHotKey(_windowHandle, id, modifiers, key))
        {
            _hotkeyActions[id] = action;
            return id;
        }

        return -1;
    }

    /// <summary>
    /// Регистрирует горячую клавишу по строке (например "F9", "Ctrl+F9")
    /// </summary>
    public int RegisterHotkey(string hotkeyString, Action action)
    {
        var (modifiers, key) = ParseHotkeyString(hotkeyString);
        if (key == 0)
            return -1;

        return RegisterHotkey(modifiers, key, action);
    }

    /// <summary>
    /// Отменяет регистрацию горячей клавиши
    /// </summary>
    public void UnregisterHotkey(int id)
    {
        if (_hotkeyActions.ContainsKey(id))
        {
            UnregisterHotKey(_windowHandle, id);
            _hotkeyActions.Remove(id);
        }
    }

    /// <summary>
    /// Отменяет регистрацию всех горячих клавиш
    /// </summary>
    public void UnregisterAllHotkeys()
    {
        foreach (var id in _hotkeyActions.Keys)
        {
            UnregisterHotKey(_windowHandle, id);
        }
        _hotkeyActions.Clear();
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action?.Invoke();
                HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(id));
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private (uint modifiers, uint key) ParseHotkeyString(string hotkeyString)
    {
        uint modifiers = MOD_NONE;
        uint key = 0;

        var parts = hotkeyString.ToUpper().Split('+');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            switch (trimmed)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= MOD_SHIFT;
                    break;
                case "WIN":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    key = GetVirtualKeyCode(trimmed);
                    break;
            }
        }

        return (modifiers, key);
    }

    private uint GetVirtualKeyCode(string keyName)
    {
        // Функциональные клавиши
        if (keyName.StartsWith("F") && int.TryParse(keyName.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
        {
            return (uint)(0x70 + fNum - 1); // VK_F1 = 0x70
        }

        // Буквы
        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
        {
            return (uint)keyName[0];
        }

        // Цифры
        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            return (uint)keyName[0];
        }

        // Специальные клавиши
        return keyName switch
        {
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "ESCAPE" or "ESC" => 0x1B,
            "BACKSPACE" => 0x08,
            "DELETE" or "DEL" => 0x2E,
            "INSERT" or "INS" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            "NUMPAD0" => 0x60,
            "NUMPAD1" => 0x61,
            "NUMPAD2" => 0x62,
            "NUMPAD3" => 0x63,
            "NUMPAD4" => 0x64,
            "NUMPAD5" => 0x65,
            "NUMPAD6" => 0x66,
            "NUMPAD7" => 0x67,
            "NUMPAD8" => 0x68,
            "NUMPAD9" => 0x69,
            _ => 0
        };
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            UnregisterAllHotkeys();
            _source?.RemoveHook(HwndHook);
            _isDisposed = true;
        }
    }
}

public class HotkeyPressedEventArgs : EventArgs
{
    public int HotkeyId { get; }

    public HotkeyPressedEventArgs(int hotkeyId)
    {
        HotkeyId = hotkeyId;
    }
}
