using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using DesktopClock;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("同频 · 中国时间")]
[assembly: AssemblyDescription("中国任务栏时间、农历与本地私人备忘")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace Tongpin
{
    public sealed class App : Application
    {
        public Repository Store; public ClockWindow Clock; public CalendarWindow Calendar; public ReminderWindow CurrentReminder; public LiveWindow Live;
        public Settings PreviewSettings; public Settings Settings { get { return PreviewSettings ?? Store.Data.Settings; } }
        public NextReminder Next; public int TodayTotal, TodayDone; DateTime nextLiveRefresh = DateTime.MinValue, nextThemeRefresh = DateTime.MinValue;
        SettingsWindow settingsWindow;
        public bool Quitting; public bool DiagnosticWindows; bool locked; Forms.NotifyIcon tray; SystemClockTimer timer; Mutex mutex; EventWaitHandle showEvent;
        RegisteredWaitHandle waiter; DateTime nextScan = DateTime.MinValue; public static string DataFolder;
        [STAThread]
        public static int Main(string[] args)
        {
            var custom = Array.IndexOf(args, "--data-dir");
            DataFolder = custom >= 0 && custom + 1 < args.Length ? Path.GetFullPath(args[custom + 1]) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TongpinChinaTime");
            if (args.Contains("--self-test")) return SelfTest.Run(DataFolder);
            try
            {
                var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var suffix = WindowsIdentity.GetCurrent().User.Value + (custom >= 0 ? "-" + DataFolder.GetHashCode().ToString("X") : "");
                bool created; app.mutex = new Mutex(true, @"Local\TongpinChinaTime-" + suffix, out created);
                app.showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TongpinChinaTimeShow-" + suffix);
                if (!created) { if (!args.Contains("--background")) app.showEvent.Set(); app.mutex.Dispose(); app.showEvent.Dispose(); return 0; }
                app.Store = new Repository(DataFolder);
                if (!File.Exists(app.Store.PathName)) app.Store.Save();
                if (args.Contains("--minimal"))
                {
                    app.Store.Data.Settings = Tongpin.Settings.Minimal(app.Store.Data.Settings); app.Store.Save();
                    app.mutex.ReleaseMutex(); app.mutex.Dispose(); app.showEvent.Dispose(); return 0;
                }
                app.DispatcherUnhandledException += (s, e) => { Log("unhandled", e.Exception.ToString()); Ui.Error(e.Exception); e.Handled = true; };
                app.Startup += (s, e) => app.Start(args);
                app.Exit += (s, e) => app.Cleanup();
                app.Run(); return 0;
            }
            catch (Exception e)
            {
                Log("startup-error", e.ToString());
                MessageBox.Show("同频未能启动，现有备忘文件已保留。\n\n" + e.Message + "\n\n数据目录：" + DataFolder, "同频 · 启动失败", MessageBoxButton.OK, MessageBoxImage.Error); return 1;
            }
        }
        void Start(string[] args)
        {
            DiagnosticWindows = args.Contains("--qa-windows");
            Theme.Apply(Settings, true);
            Clock = new ClockWindow(this); Calendar = new CalendarWindow(this); Live = new LiveWindow(this);
            tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), Text = "同频 · 中国时间", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开中国日历", null, (s, e) => Calendar.Reveal());
            menu.Items.Add("外观、位置与实时信息", null, (s, e) => OpenSettings());
            menu.Items.Add("显示 / 收起实时卡片", null, (s, e) => ToggleLive());
            menu.Items.Add("退出同频", null, (s, e) => Quit()); tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == Forms.MouseButtons.Left) Calendar.Reveal(); };
            waiter = ThreadPool.RegisterWaitForSingleObject(showEvent, (s, timedOut) => Dispatcher.BeginInvoke(new Action(() => Calendar.Reveal())), null, Timeout.Infinite, false);
            SystemEvents.SessionSwitch += SessionSwitch;
            SystemEvents.PowerModeChanged += PowerChanged;
            timer = new SystemClockTimer(); timer.SecondChanged += (s, e) => { if (!Quitting) Dispatcher.BeginInvoke(new Action(Tick)); }; timer.Start();
            RefreshLive(); Clock.Show(); Clock.Tick(); Live.Tick();
            if (!args.Contains("--background")) Calendar.Reveal();
            if (args.Contains("--test-alert")) TestReminder();
            if (args.Contains("--capture-ui"))
            {
                var captureTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                captureTimer.Tick += (s, e) => { captureTimer.Stop(); Capture(Calendar, "calendar-preview.png"); Capture(Clock, "taskbar-clock.png"); Capture(Live, "live-card.png"); if (CurrentReminder != null) Capture(CurrentReminder, "reminder-preview.png"); WriteVisualEvidence(); };
                captureTimer.Start();
            }
            Log("started", "version=2.0.0; systemTimezone=" + TimeZoneInfo.Local.Id + "; china=" + China.Now.ToString("s"));
        }
        void SessionSwitch(object sender, SessionSwitchEventArgs e)
        { Dispatcher.BeginInvoke(new Action(() => { locked = e.Reason == SessionSwitchReason.SessionLock; if (locked) { CurrentReminder?.Hide(); Calendar.Hide(); Live.Hide(); } else { nextScan = DateTime.MinValue; CurrentReminder?.Show(); } })); }
        void PowerChanged(object sender, PowerModeChangedEventArgs e)
        { if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(new Action(() => { TimeZoneInfo.ClearCachedData(); nextScan = DateTime.MinValue; Tick(); })); }
        void Tick()
        {
            if (Quitting) return;
            if (DateTime.UtcNow >= nextThemeRefresh) { nextThemeRefresh = DateTime.UtcNow.AddSeconds(3); if (Theme.Apply(Settings)) ApplyAppearance(); }
            if (DateTime.UtcNow >= nextLiveRefresh) RefreshLive();
            Clock.Tick(); if (!locked) Live.Tick(); if (Calendar.IsVisible) Calendar.Tick();
            if (DateTime.UtcNow >= nextScan) { nextScan = DateTime.UtcNow.AddSeconds(10); ScanReminders(); }
        }
        public void SaveAndRefresh() { Store.Save(); RefreshLive(); Calendar.Refresh(); ScanReminders(); }
        public void RefreshLive()
        {
            nextLiveRefresh = DateTime.UtcNow.AddSeconds(15); Next = LiveData.Next(Store.Data, DateTime.UtcNow);
            var day = Schedule.Day(Store.Data, China.Now.Date); TodayTotal = day.Count; TodayDone = day.Count(o => Store.Data.Completed.Contains(o.Key));
        }
        public void ApplyAppearance()
        {
            Theme.Apply(Settings); foreach (Window w in Windows) Native.Decorate(w);
            Clock?.ApplyStyle(); Clock?.Tick(); Live?.ApplyStyle(); Live?.Tick(); Calendar?.Refresh(); Calendar?.Tick();
        }
        public void OpenSettings()
        {
            if (settingsWindow != null) { settingsWindow.Activate(); return; }
            settingsWindow = new SettingsWindow(this, Calendar); settingsWindow.Closed += (s, e) => settingsWindow = null; settingsWindow.Show(); settingsWindow.Activate();
        }
        public void ToggleLive() { Settings.LiveVisible = !Settings.LiveVisible; PersistDisplay(); }
        public void PersistDisplay() { if (PreviewSettings == null) Store.Save(); ApplyAppearance(); }
        public void WriteVisualEvidence()
        {
            var values = Windows.Cast<Window>().Where(w => w.IsVisible).Select(w => { Native.RECT rect; Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(w).Handle, out rect); return new { type = w.GetType().Name, w.AllowsTransparency, rect.Left, rect.Top, rect.Width, rect.Height }; }).ToArray();
            File.WriteAllText(Path.Combine(DataFolder, "visual-state.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { utc = DateTime.UtcNow.ToString("o"), Theme.Dark, Theme.TaskbarDark, Theme.GlassEnabled, Settings, windows = values }));
        }
        public void CaptureAll() { foreach (Window w in Windows) if (w.IsVisible) Capture(w, "qa-" + w.GetType().Name + ".png"); WriteVisualEvidence(); }
        public void ScanReminders()
        {
            if (locked) return;
            if (CurrentReminder != null && !CurrentReminder.IsTest)
            {
                var current = CurrentReminder.Occurrence;
                var latest = Store.Data.Memos.FirstOrDefault(x => x.Id == current.Memo.Id && !x.Deleted && x.Remind);
                if (latest == null || latest.Modified != current.Memo.Modified || Store.Data.Completed.Contains(current.Key)) CurrentReminder.Close();
            }
            if (CurrentReminder != null) return;
            var due = Schedule.Due(Store.Data, DateTime.UtcNow).FirstOrDefault();
            if (due == null) return;
            CurrentReminder = new ReminderWindow(this, due, false); CurrentReminder.Show();
            if (Store.Data.Settings.Sound) System.Media.SystemSounds.Asterisk.Play();
            Log("reminder-shown", due.Memo.Id + "|" + China.Key(due.When));
        }
        public void TestReminder()
        {
            if (CurrentReminder != null) { CurrentReminder.Activate(); return; }
            CurrentReminder = new ReminderWindow(this, null, true); CurrentReminder.Show();
            if (Store.Data.Settings.Sound) System.Media.SystemSounds.Asterisk.Play();
            Log("test-reminder", "shown");
        }
        public void HandleReminder(ReminderWindow window, string action)
        {
            if (!window.IsTest)
            {
                var key = window.Occurrence.Key;
                if (action == "snooze") Store.Data.Snoozes[key] = DateTime.UtcNow.AddMinutes(10).Ticks;
                else { if (!Store.Data.Acknowledged.Contains(key)) Store.Data.Acknowledged.Add(key); Store.Data.Snoozes.Remove(key); if (action == "done" && !Store.Data.Completed.Contains(key)) Store.Data.Completed.Add(key); }
                Store.Save(); RefreshLive(); Calendar.Refresh();
            }
            window.Close(); nextScan = DateTime.UtcNow.AddSeconds(1);
        }
        public void Quit() { Quitting = true; Shutdown(); }
        public static void Capture(Window w, string name)
        {
            if (w == null || !w.IsVisible) return; w.UpdateLayout();
            if (w.ActualWidth < 1 || w.ActualHeight < 1) return;
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(w.ActualWidth * 2), (int)(w.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
            bmp.Render(w); var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using (var file = File.Create(Path.Combine(DataFolder, name))) enc.Save(file);
        }
        void Cleanup()
        {
            Quitting = true; timer?.Dispose(); tray?.Dispose(); waiter?.Unregister(null); showEvent?.Dispose();
            SystemEvents.SessionSwitch -= SessionSwitch; SystemEvents.PowerModeChanged -= PowerChanged;
            try { mutex?.ReleaseMutex(); } catch (ApplicationException) { } mutex?.Dispose();
            Log("stopped", "clean");
        }
        public static void Log(string kind, string text)
        {
            try { Directory.CreateDirectory(DataFolder); File.AppendAllText(Path.Combine(DataFolder, "app.log"), DateTime.UtcNow.ToString("o") + " " + kind + " " + text + Environment.NewLine); } catch { }
        }
    }
}
