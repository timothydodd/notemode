using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace NoteMode.Views;

/// <summary>
/// Shortcuts are written once, Windows style ("Ctrl+S" in XAML, <see cref="Command"/> in code). On
/// macOS they become the Mac ones: Cmd instead of Ctrl, plus the few that differ there (Cmd+H hides
/// the app, so Replace moves to Cmd+Option+F like TextEdit; Redo is Cmd+Shift+Z; Exit is Cmd+Q).
/// <see cref="Install"/> rewrites every key binding, menu gesture and "Ctrl+" tooltip as it is set,
/// so XAML and templates need no per-platform copies. Elsewhere it does nothing.
/// </summary>
internal static class PlatformShortcuts
{
    /// <summary>The platform's command modifier: Ctrl on Windows and Linux, Cmd (Meta) on macOS.</summary>
    public static KeyModifiers Command =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>Call once at startup, before any window is created.</summary>
    public static void Install()
    {
        if (Command != KeyModifiers.Meta)
            return;

        KeyBinding.GestureProperty.Changed.AddClassHandler<KeyBinding>((binding, _) =>
        {
            if (binding.Gesture is { } gesture && ToMac(gesture) is { } mac)
                binding.Gesture = mac;
        });
        MenuItem.InputGestureProperty.Changed.AddClassHandler<MenuItem>((item, _) =>
        {
            if (item.InputGesture is { } gesture && ToMac(gesture) is { } mac)
                item.InputGesture = mac;
        });
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (ToolTip.GetTip(control) is string tip && tip.Contains("Ctrl+", StringComparison.Ordinal))
                ToolTip.SetTip(control, tip.Replace("Ctrl+", "Cmd+", StringComparison.Ordinal));
        });
    }

    /// <summary>The macOS form of a Windows gesture, or null when it is already right.</summary>
    private static KeyGesture? ToMac(KeyGesture gesture)
    {
        var (key, modifiers) = (gesture.Key, gesture.KeyModifiers);
        if (key == Key.F4 && modifiers == KeyModifiers.Alt)
            return new KeyGesture(Key.Q, KeyModifiers.Meta);
        if (!modifiers.HasFlag(KeyModifiers.Control))
            return null;
        if (key == Key.H && modifiers == KeyModifiers.Control)
            return new KeyGesture(Key.F, KeyModifiers.Meta | KeyModifiers.Alt);
        if (key == Key.Y && modifiers == KeyModifiers.Control)
            return new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift);
        return new KeyGesture(key, (modifiers & ~KeyModifiers.Control) | KeyModifiers.Meta);
    }
}
