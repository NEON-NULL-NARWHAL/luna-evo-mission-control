// LUNA EVO · LunaEvo.ConfigManager/DesktopNotifier.cs
// [UI] Windows notifications (the server crashed, someone is guessing the admin password...), via the notification area.
// Used by: MainWindow.Notify
// Guide:   Meat and Potatoes/17-Notifications.md

using System.Runtime.InteropServices;

namespace LunaEvo.ConfigManager;

/// <summary>
/// Uses Shell_NotifyIcon's balloon, which Windows 10 and 11 show as a normal notification (and keep in the
/// notification centre). No package or app registration needed. The LUNA EVO icon appears in the notification area
/// after the first one and is removed when the app closes. Does nothing on other systems.
/// </summary>
internal static class DesktopNotifier
{
    private const uint NimAdd = 0, NimModify = 1, NimDelete = 2;
    private const uint NifIcon = 0x2, NifTip = 0x4, NifInfo = 0x10;
    private const uint NiifInfo = 0x1, NiifWarning = 0x2;
    private const uint IconId = 1;

    private static IntPtr _window;
    private static IntPtr _icon;
    private static bool _added;

    /// <summary>The main window's handle; set once it's shown.</summary>
    public static void Attach(IntPtr window) => _window = window;

    public static void Show(string title, string message, bool warning)
    {
        if (!OperatingSystem.IsWindows() || _window == IntPtr.Zero)
            return;
        var data = NewData();
        if (!_added)
        {
            data.uFlags = NifIcon | NifTip;
            _added = Shell_NotifyIconW(NimAdd, ref data);
            if (!_added)
                return;
        }
        data.uFlags = NifInfo | NifIcon | NifTip;
        data.szInfoTitle = Cut(title, 63);
        data.szInfo = Cut(message, 255);
        data.dwInfoFlags = warning ? NiifWarning : NiifInfo;
        Shell_NotifyIconW(NimModify, ref data);
    }

    /// <summary>Removes the notification-area icon. Called when the app closes.</summary>
    public static void Remove()
    {
        if (!_added || !OperatingSystem.IsWindows())
            return;
        var data = NewData();
        Shell_NotifyIconW(NimDelete, ref data);
        _added = false;
    }

    private static NotifyIconData NewData()
    {
        if (_icon == IntPtr.Zero && Environment.ProcessPath is { } exe)
            _icon = ExtractIconW(IntPtr.Zero, exe, 0); // the exe's own icon (ApplicationIcon)
        if (_icon == IntPtr.Zero || _icon == (IntPtr)1)
            _icon = LoadIconW(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _window,
            uID = IconId,
            hIcon = _icon,
            szTip = "LUNA EVO Config Manager",
            szInfo = "",
            szInfoTitle = "",
        };
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIconW(IntPtr instance, string exeFileName, int iconIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIconW(IntPtr instance, IntPtr iconName);
}
