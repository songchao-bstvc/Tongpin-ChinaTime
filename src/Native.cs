using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Tongpin
{
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; public int Width { get { return Right - Left; } } public int Height { get { return Bottom - Top; } } }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int style);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int Left, Right, Top, Bottom; }
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(System.Drawing.Point point, uint flags);
        delegate bool EnumChildProc(IntPtr h, IntPtr param);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr param);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int capacity);
        public static RECT ClockRect;
        static DateTime lastLocate = DateTime.MinValue;
        static bool locating;
        static bool loggedElements;
        static int taskButtonsRight;
        public static void SetToolWindow(Window window)
        {
            var h = new WindowInteropHelper(window).Handle;
            SetWindowLong(h, -20, (GetWindowLong(h, -20) | 0x80) & ~0x40000);
        }
        public static void Decorate(Window w)
        {
            var h = new WindowInteropHelper(w).Handle;
            if (h == IntPtr.Zero || w.AllowsTransparency) return;
            try
            {
                int dark = Theme.Dark ? 1 : 0, corner = 2, backdrop = Theme.GlassEnabled ? 3 : 1;
                DwmSetWindowAttribute(h, 20, ref dark, 4); DwmSetWindowAttribute(h, 33, ref corner, 4);
                int result = DwmSetWindowAttribute(h, 38, ref backdrop, 4);
                var margins = new MARGINS { Left = Theme.GlassEnabled && result == 0 ? -1 : 0 };
                DwmExtendFrameIntoClientArea(h, ref margins);
                var source = HwndSource.FromHwnd(h); if (source?.CompositionTarget != null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
                w.Background = Theme.GlassEnabled && result == 0 ? Ui.Glass : Ui.Background;
                if (((App)Application.Current).DiagnosticWindows) App.Log("window-material", w.GetType().Name + ";dark=" + dark + ";backdrop=" + backdrop + ";hresult=" + result);
            }
            catch (DllNotFoundException) { w.Background = Ui.Background; }
            catch (EntryPointNotFoundException) { w.Background = Ui.Background; }
        }
        public static System.Windows.Forms.Screen ScreenFor(Settings s)
        { return System.Windows.Forms.Screen.AllScreens.FirstOrDefault(x => x.DeviceName == s.Monitor) ?? System.Windows.Forms.Screen.PrimaryScreen; }
        public static double Scale(System.Windows.Forms.Screen screen)
        {
            try { uint x, y; if (GetDpiForMonitor(MonitorFromPoint(new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1), 2), 0, out x, out y) == 0) return x / 96.0; } catch { }
            return 1;
        }
        public static bool ForegroundFullscreen(Window target = null)
        {
            var h = GetForegroundWindow(); uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid == Process.GetCurrentProcess().Id || h == FindWindow("Progman", null) || h == FindWindow("Shell_TrayWnd", null)) return false;
            RECT r; if (!GetWindowRect(h, out r)) return false;
            var b = System.Windows.Forms.Screen.FromHandle(h).Bounds;
            if (target != null && System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(target).Handle).DeviceName != System.Windows.Forms.Screen.FromHandle(h).DeviceName) return false;
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }
        public static void LocateClock(Action<RECT> found)
        {
            if (locating || (DateTime.UtcNow - lastLocate).TotalSeconds < 20) return;
            locating = true; lastLocate = DateTime.UtcNow;
            Task.Run(() => {
                try
                {
                    var h = FindWindow("Shell_TrayWnd", null); if (h == IntPtr.Zero) return;
                    RECT nativeClock = new RECT();
                    EnumChildWindows(h, (child, param) => {
                        var cls = new System.Text.StringBuilder(256); GetClassName(child, cls, 256);
                        if (cls.ToString() == "TrayClockWClass" || cls.ToString() == "ClockButton")
                        { RECT r; if (GetWindowRect(child, out r) && r.Width > 30 && r.Height > 15) nativeClock = r; }
                        return true;
                    }, IntPtr.Zero);
                    if (nativeClock.Width > 30)
                    { Application.Current.Dispatcher.BeginInvoke(new Action(() => { ClockRect = nativeClock; found(nativeClock); })); return; }
                    var root = AutomationElement.FromHandle(h);
                    var taskButtons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer"));
                    int buttonsRight = 0;
                    for (int n = 0; n < taskButtons.Count; n++) buttonsRight = Math.Max(buttonsRight, (int)taskButtons[n].Current.BoundingRectangle.Right);
                    taskButtonsRight = buttonsRight;
                    if (!loggedElements)
                    {
                        loggedElements = true;
                        var all = root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
                        var lines = new System.Collections.Generic.List<string>();
                        RECT tr; GetWindowRect(h, out tr);
                        for (int n = 0; n < all.Count; n++)
                        {
                            var a = all[n].Current; var r = a.BoundingRectangle;
                            if (a.ClassName == "Taskbar.TaskListButtonAutomationPeer") taskButtonsRight = Math.Max(taskButtonsRight, (int)r.Right);
                            if (r.Left > tr.Right - 500) lines.Add(a.AutomationId + "|" + a.ClassName + "|" + a.ControlType.ProgrammaticName + "|" + r);
                        }
                        if (((App)Application.Current).DiagnosticWindows) System.IO.File.WriteAllLines(System.IO.Path.Combine(App.DataFolder, "taskbar-layout.txt"), lines);
                    }
                    var elements = root.FindAll(TreeScope.Descendants, new OrCondition(
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTray.DateTimeIcon"),
                        new PropertyCondition(AutomationElement.ClassNameProperty, "SystemTray.OmniButtonRight"),
                        new PropertyCondition(AutomationElement.ClassNameProperty, "ClockButton"),
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "ClockButton")));
                    for (int i = 0; i < elements.Count; i++)
                    {
                        var r = elements[i].Current.BoundingRectangle;
                        var info = elements[i].Current;
                        if (info.ClassName == "SystemTray.OmniButtonRight" && !System.Text.RegularExpressions.Regex.IsMatch(info.Name ?? "", @"\d{1,2}:\d{2}|时钟|日期|Clock|Date")) continue;
                        if (r.Width > 30 && r.Height > 15 && !double.IsInfinity(r.Left))
                        {
                            var rect = new RECT { Left = (int)r.Left, Top = (int)r.Top, Right = (int)r.Right, Bottom = (int)r.Bottom };
                            Application.Current.Dispatcher.BeginInvoke(new Action(() => { ClockRect = rect; found(rect); })); return;
                        }
                    }
                }
                catch (Exception e) { App.Log("clock-locate", e.GetType().Name); }
                finally { locating = false; }
            });
        }
        public static RECT TargetClock()
        {
            RECT taskbar; var h = FindWindow("Shell_TrayWnd", null);
            if (h != IntPtr.Zero && GetWindowRect(h, out taskbar))
            {
                if (ClockRect.Width > 30 && ClockRect.Top >= taskbar.Top && ClockRect.Bottom <= taskbar.Bottom) return ClockRect;
                var scale = GetDpiForWindow(h) / 96.0;
                RECT tray;
                var trayHandle = FindWindowEx(h, IntPtr.Zero, "TrayNotifyWnd", null);
                if (trayHandle != IntPtr.Zero && GetWindowRect(trayHandle, out tray))
                {
                    var right = tray.Left - (int)(4 * scale); var left = right - (int)(122 * scale);
                    if (taskButtonsRight > 0 && left < taskButtonsRight + 8)
                        return new RECT { Left = tray.Right - (int)(126 * scale), Right = tray.Right - (int)(4 * scale), Top = taskbar.Top - (int)(48 * scale), Bottom = taskbar.Top };
                    return new RECT { Left = left, Right = right, Top = taskbar.Top, Bottom = taskbar.Bottom };
                }
                return new RECT { Left = taskbar.Right - (int)(140 * scale), Right = taskbar.Right - (int)(28 * scale), Top = taskbar.Top, Bottom = taskbar.Bottom };
            }
            var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            return new RECT { Left = b.Right - 160, Right = b.Right - 30, Top = b.Bottom - 48, Bottom = b.Bottom };
        }
        public static void PositionClock(Window w, Settings settings)
        {
            var h = new WindowInteropHelper(w).Handle; if (h == IntPtr.Zero) return;
            var screen = ScreenFor(settings); var scale = Scale(screen);
            if (settings.Placement == "taskbar" && screen.Primary)
            {
                var r = TargetClock(); int width = (int)(122 * settings.ClockScale * scale);
                int height = Math.Max((int)(42 * scale), Math.Min(r.Height, (int)(48 * settings.ClockScale * scale)));
                int left = r.Right - width, top = r.Top + (r.Height - height) / 2;
                if (taskButtonsRight > 0 && left < taskButtonsRight + 8) { top = screen.WorkingArea.Bottom - height - (int)(6 * scale); left = screen.WorkingArea.Right - width - (int)(12 * scale); }
                left = Math.Max(screen.Bounds.Left, Math.Min(screen.Bounds.Right - width, left + settings.OffsetX));
                top = Math.Max(screen.Bounds.Top, Math.Min(screen.Bounds.Bottom - height, top + settings.OffsetY));
                SetWindowPos(h, new IntPtr(-1), left, top, width, height, 0x10 | 0x40);
            }
            else PositionWidget(w, settings, false, 150 * settings.ClockScale, 60 * settings.ClockScale);
        }
        public static void PositionWidget(Window w, Settings s, bool live, double width, double height)
        {
            var h = new WindowInteropHelper(w).Handle; if (h == IntPtr.Zero) return;
            var screen = ScreenFor(s); var scale = Scale(screen);
            var mode = live ? s.LivePlacement : s.Placement; if (mode == "taskbar") mode = "bottom-right";
            var r = DisplayLayout.Place(screen.WorkingArea, (int)(width * scale), (int)(height * scale), mode, live ? s.LiveX : s.CustomX, live ? s.LiveY : s.CustomY, (int)(16 * scale));
            if (live && mode != "custom")
            {
                var clock = ((App)Application.Current).Clock; RECT anchor;
                if (clock != null && GetWindowRect(new WindowInteropHelper(clock).Handle, out anchor) && r.IntersectsWith(new System.Drawing.Rectangle(anchor.Left, anchor.Top, anchor.Width, anchor.Height)))
                {
                    int gap = (int)(12 * scale);
                    int y = mode.StartsWith("top") ? anchor.Bottom + gap : anchor.Top - r.Height - gap;
                    r.Y = Math.Max(screen.WorkingArea.Top, Math.Min(screen.WorkingArea.Bottom - r.Height, y));
                }
            }
            SetWindowPos(h, new IntPtr(live && !s.LiveTopmost ? -2 : -1), r.Left, r.Top, r.Width, r.Height, 0x10 | 0x40);
        }
        public static void RememberPosition(Window w, Settings s, bool live)
        {
            RECT r; var h = new WindowInteropHelper(w).Handle; if (!GetWindowRect(h, out r)) return;
            var screen = System.Windows.Forms.Screen.FromHandle(h); var area = screen.WorkingArea; s.Monitor = screen.DeviceName;
            double x = Math.Max(0, Math.Min(1, (r.Left - area.Left) / (double)Math.Max(1, area.Width - r.Width)));
            double y = Math.Max(0, Math.Min(1, (r.Top - area.Top) / (double)Math.Max(1, area.Height - r.Height)));
            if (live) { s.LivePlacement = "custom"; s.LiveX = x; s.LiveY = y; }
            else { s.Placement = "custom"; s.CustomX = x; s.CustomY = y; s.OffsetX = s.OffsetY = 0; }
        }
        public static void PositionPanel(Window w, double width, double height)
        {
            var app = (App)Application.Current;
            var screen = ScreenFor(app.Settings);
            var b = screen.WorkingArea; var source = PresentationSource.FromVisual(w);
            var scale = Scale(screen);
            var pxW = Math.Min((int)(width * scale), b.Width - 24); var pxH = Math.Min((int)(height * scale), b.Height - 24);
            RECT anchor; int x = b.Right - pxW - 12, y = b.Bottom - pxH - 12;
            if (app.Clock != null && GetWindowRect(new WindowInteropHelper(app.Clock).Handle, out anchor))
            {
                x = anchor.Left < b.Left + b.Width / 2 ? anchor.Left : anchor.Right - pxW;
                y = anchor.Top < b.Top + b.Height / 2 ? anchor.Bottom + 12 : anchor.Top - pxH - 12;
                x = Math.Max(b.Left + 12, Math.Min(b.Right - pxW - 12, x)); y = Math.Max(b.Top + 12, Math.Min(b.Bottom - pxH - 12, y));
            }
            SetWindowPos(new WindowInteropHelper(w).Handle, new IntPtr(-1), x, y, pxW, pxH, 0x40);
        }
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool StartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) return !string.IsNullOrWhiteSpace(key?.GetValue("TongpinChinaTime") as string);
        }
        public static string StartupCommand { get { return "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --background"; } }
        public static void SetStartup(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            { if (enabled) key.SetValue("TongpinChinaTime", StartupCommand); else key.DeleteValue("TongpinChinaTime", false); }
        }
    }
}
