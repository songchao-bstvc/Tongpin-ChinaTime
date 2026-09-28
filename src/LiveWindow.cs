using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Tongpin
{
    public sealed class LiveWindow : Window
    {
        readonly App host; readonly StackPanel body; readonly TextBlock clock, day, local, lunar, next, together, progressText;
        readonly Border localCard, lunarCard, nextCard, togetherCard; readonly Grid progressBox; readonly Border progressBar;
        bool dragging; double naturalHeight = 370; DateTime lunarDay = DateTime.MinValue; string lunarCache;
        public LiveWindow(App app)
        {
            host = app; Title = "同频 · 实时信息卡片"; Width = 308; Height = 370; WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize; ShowActivated = false; ShowInTaskbar = app.DiagnosticWindows; Topmost = true; Ui.Style(this);
            var frame = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(19, 16, 19, 18) }; Content = frame;
            body = new StackPanel(); frame.Child = body;
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            var close = Ui.Button("×", () => host.ToggleLive()); close.Padding = new Thickness(7, 1, 7, 1); close.Margin = new Thickness(0); close.Background = Brushes.Transparent; close.ToolTip = "收起实时卡片"; DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            header.Children.Add(Ui.Text("同频   /   此刻，在一起", 11, Ui.Muted)); body.Children.Add(header);
            WindowDrag.Attach(this, header, () => true, value => dragging = value, () => { Native.RememberPosition(this, host.Settings, true); host.PersistDisplay(); });
            clock = Ui.Text("", 35); Typography.SetNumeralAlignment(clock, FontNumeralAlignment.Tabular); body.Children.Add(clock);
            day = Ui.Text("", 12, Ui.Muted); day.Margin = new Thickness(1, 0, 0, 14); body.Children.Add(day);
            local = Ui.Text("", 12); localCard = MakeRow("本地时间", local);
            lunar = Ui.Text("", 12); lunarCard = MakeRow("中国日历", lunar);
            next = Ui.Text("", 13, Ui.Accent); nextCard = MakeRow("下一条提醒", next); nextCard.Cursor = Cursors.Hand;
            nextCard.MouseLeftButtonUp += (s, e) => { if (host.Next != null) host.Calendar.GoTo(host.Next.Occurrence.When.Date); host.Calendar.Reveal(); };
            together = Ui.Text("", 12, Ui.Green); togetherCard = MakeRow("我们的日子", together);
            progressBox = new Grid { Margin = new Thickness(0, 10, 0, 6) }; progressBox.RowDefinitions.Add(new RowDefinition()); progressBox.RowDefinitions.Add(new RowDefinition());
            progressText = Ui.Text("", 11, Ui.Muted); progressBox.Children.Add(progressText);
            var track = new Border { Background = Ui.Control, CornerRadius = new CornerRadius(2), Height = 3, Margin = new Thickness(0, 9, 0, 0) }; Grid.SetRow(track, 1); progressBox.Children.Add(track);
            progressBar = new Border { Background = Ui.Accent, CornerRadius = new CornerRadius(2), Height = 3, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 9, 0, 0) }; Grid.SetRow(progressBar, 1); progressBox.Children.Add(progressBar); body.Children.Add(progressBox);
            var actions = Ui.Row(Ui.Button("打开日历", () => host.Calendar.Reveal()), Ui.Button("自定义", () => host.OpenSettings())); actions.Margin = new Thickness(0, 8, 0, 0); foreach (Button b in actions.Children) { b.FontSize = 11; b.Padding = new Thickness(10, 6, 10, 6); } body.Children.Add(actions);
            SourceInitialized += (s, e) => { if (!app.DiagnosticWindows) Native.SetToolWindow(this); ApplyStyle(); };
            Closing += (s, e) => { if (!app.Quitting) { e.Cancel = true; host.Settings.LiveVisible = false; host.PersistDisplay(); Hide(); } };
        }
        Border MakeRow(string caption, TextBlock value)
        {
            var row = new StackPanel(); var label = Ui.Text(caption, 10, Ui.Muted); label.Margin = new Thickness(0, 0, 0, 3); row.Children.Add(label); row.Children.Add(value);
            var card = new Border { Child = row, Margin = new Thickness(0, 0, 0, 11) }; body.Children.Add(card); return card;
        }
        public void ApplyStyle()
        {
            var s = host.Settings; Topmost = s.LiveTopmost; body.Opacity = SystemParameters.HighContrast ? 1 : s.LiveOpacity;
            localCard.Visibility = s.LiveLocal ? Visibility.Visible : Visibility.Collapsed;
            lunarCard.Visibility = s.LiveLunar ? Visibility.Visible : Visibility.Collapsed;
            nextCard.Visibility = s.LiveNext ? Visibility.Visible : Visibility.Collapsed;
            togetherCard.Visibility = s.LiveTogether ? Visibility.Visible : Visibility.Collapsed;
            progressBox.Visibility = s.LiveProgress ? Visibility.Visible : Visibility.Collapsed;
            Native.Decorate(this);
        }
        public void Tick()
        {
            if (dragging) return;
            var s = host.Settings;
            if (!s.LiveVisible || (s.HideFullscreen && Native.ForegroundFullscreen(this))) { if (IsVisible) Hide(); return; }
            if (!IsVisible) { Show(); ApplyStyle(); Theme.FadeIn(this, s); }
            var now = China.Now; var utc = DateTime.UtcNow;
            if (lunarDay != now.Date) { lunarDay = now.Date; lunarCache = China.Details(now); }
            clock.Text = now.ToString(s.Seconds ? "HH:mm:ss" : "HH:mm");
            day.Text = "中国  " + now.ToString("M月d日") + "  " + China.Week(now) + "  ·  UTC+8";
            double offset = (China.Zone.GetUtcOffset(utc) - TimeZoneInfo.Local.GetUtcOffset(utc)).TotalHours;
            local.Text = DateTime.Now.ToString("M月d日 HH:mm") + "   ·   中国" + (offset >= 0 ? "快 " : "慢 ") + Math.Abs(offset).ToString("0.#") + " 小时";
            lunar.Text = lunarCache;
            next.Text = LiveData.Countdown(host.Next, utc, s.Seconds);
            if (host.Next != null) next.Text += "\n" + (s.LivePrivate ? "私人备忘 · 点击查看" : host.Next.Occurrence.Memo.Title);
            together.Text = LiveData.Together(s.TogetherSince, now);
            var fraction = now.TimeOfDay.TotalSeconds / 86400;
            progressText.Text = "中国今日 " + Math.Floor(fraction * 100) + "%   ·   备忘 " + host.TodayDone + " / " + host.TodayTotal + " 已完成";
            progressBar.Width = Math.Max(1, 268 * fraction);
            body.Measure(new Size(268, double.PositiveInfinity)); naturalHeight = body.DesiredSize.Height + 38;
            Native.PositionWidget(this, s, true, 308, naturalHeight);
        }
    }
}
