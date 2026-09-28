using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Tongpin
{
    public sealed class ClockWindow : Window
    {
        readonly App host; readonly TextBlock time, date; readonly Border plate, feedback;
        readonly SolidColorBrush clockInk = Ui.AdaptiveBrush("#F4F5F7"), clockMuted = Ui.AdaptiveBrush("#F4F5F7");
        bool dragging; DateTime detailDay = DateTime.MinValue; string lunar = "", details = "";
        public ClockWindow(App app)
        {
            host = app; Title = "同频 · 中国任务栏时钟"; Width = 122; Height = 48;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
            Ui.Style(this); ShowInTaskbar = app.DiagnosticWindows; ShowActivated = false; Topmost = true; Background = Brushes.Transparent;
            var content = new Grid(); Content = content;
            // Alpha 1 keeps the complete text area clickable while remaining visually transparent.
            plate = new Border { Background = Ui.Brush("#01000000"), CornerRadius = new CornerRadius(7), Margin = new Thickness(2, 3, 2, 3) }; content.Children.Add(plate);
            feedback = new Border { CornerRadius = new CornerRadius(7), Margin = new Thickness(2, 3, 2, 3), Opacity = 0, IsHitTestVisible = false }; content.Children.Add(feedback);
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = 116 };
            time = new TextBlock { FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12.5, FontWeight = FontWeights.Medium, Foreground = clockInk, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
            Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);
            date = new TextBlock { FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 10.5, Foreground = clockMuted, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            stack.Children.Add(time); stack.Children.Add(date); content.Children.Add(new Viewbox { Child = stack, Stretch = Stretch.Uniform, Margin = new Thickness(5, 3, 5, 3) });
            Cursor = Cursors.Hand;
            MouseEnter += (s, e) => Hover(true); MouseLeave += (s, e) => Hover(false);
            WindowDrag.Attach(this, this, () => !host.Settings.PositionLocked || Keyboard.Modifiers == ModifierKeys.Control, value => dragging = value,
                () => { Native.RememberPosition(this, host.Settings, false); host.PersistDisplay(); },
                () => { if (host.Calendar.IsVisible) host.Calendar.Hide(); else host.Calendar.Reveal(); });
            var menu = new ContextMenu();
            menu.Items.Add(Item("打开中国日历", () => host.Calendar.Reveal()));
            menu.Items.Add(Item("外观、位置与实时信息…", () => host.OpenSettings()));
            menu.Items.Add(Item("显示 / 收起实时卡片", () => host.ToggleLive()));
            var positions = new MenuItem { Header = "移动时钟到" };
            for (int n = 0; n < DisplayLayout.Placements.Length; n++)
            {
                var key = DisplayLayout.Placements[n];
                positions.Items.Add(Item(DisplayLayout.Names[n], () => { host.Settings.Placement = key; host.Settings.OffsetX = host.Settings.OffsetY = 0; host.Settings.PositionLocked = key != "custom"; host.PersistDisplay(); }));
            }
            menu.Items.Add(positions);
            var unlock = Item("解锁拖动", () => { host.Settings.PositionLocked = !host.Settings.PositionLocked; host.PersistDisplay(); }); menu.Items.Add(unlock);
            menu.Opened += (s, e) => unlock.Header = host.Settings.PositionLocked ? "解锁拖动（也可按住 Ctrl 拖动）" : "锁定当前位置";
            menu.Items.Add(new Separator()); menu.Items.Add(Item("退出同频", () => host.Quit())); ContextMenu = menu;
            SourceInitialized += (s, e) => { if (!app.DiagnosticWindows) Native.SetToolWindow(this); ApplyStyle(); };
            Closing += (s, e) => { if (!host.Quitting) e.Cancel = true; };
        }
        static MenuItem Item(string label, Action action) { var item = new MenuItem { Header = label }; item.Click += (s, e) => { try { action(); } catch (Exception ex) { Ui.Error(ex); } }; return item; }
        void Hover(bool entered)
        {
            double target = entered ? 1 : 0;
            if (host.Settings.Motion && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
                feedback.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(130)));
            else { feedback.BeginAnimation(OpacityProperty, null); feedback.Opacity = target; }
        }
        public void ApplyStyle()
        {
            bool dark = host.Settings.Placement == "taskbar" ? Theme.TaskbarDark : Theme.Dark;
            Ui.SetColor(clockInk, SystemParameters.HighContrast ? SystemColors.WindowTextColor : (Color)ColorConverter.ConvertFromString(dark ? "#F4F5F7" : "#202327"));
            Ui.SetColor(clockMuted, clockInk.Color);
            date.Opacity = .86;
            plate.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : host.Settings.ClockStyle == "soft" ? Ui.Brush(dark ? "#B020242B" : "#C4F5F7FA") : Ui.Brush("#01000000");
            feedback.Background = Ui.Brush(dark ? "#14FFFFFF" : "#10000000");
            Opacity = SystemParameters.HighContrast ? 1 : host.Settings.ClockOpacity;
        }
        public void Tick()
        {
            if (dragging) return;
            var s = host.Settings; var now = China.Now;
            if (now.Date != detailDay) { detailDay = now.Date; lunar = China.LunarText(now); details = China.Details(now); }
            time.Text = now.ToString(s.Seconds ? "HH:mm:ss" : "HH:mm");
            var lines = new List<string>();
            if (s.DateVisible) lines.Add(now.ToString("M月d日") + "  周" + "日一二三四五六"[(int)now.DayOfWeek]);
            if (s.CompactLunar) lines.Add(lunar);
            if (s.CompactLocal) lines.Add("本地 " + DateTime.Now.ToString("M/d HH:mm"));
            if (s.CompactNext) lines.Add(LiveData.Countdown(host.Next, DateTime.UtcNow, s.Seconds));
            if (s.CompactTogether) lines.Add(LiveData.Together(s.TogetherSince, now));
            date.Visibility = lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (lines.Count > 0) date.Text = lines[(int)((DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond / 8) % lines.Count)];
            ToolTip = "中国 " + now.ToString("yyyy年M月d日 ") + China.Week(now) + "\n" + details + "\n本地 " + DateTime.Now.ToString("M月d日 HH:mm") + "\n" + LiveData.Countdown(host.Next, DateTime.UtcNow, false) + "\n点击：日历与备忘  ·  右键：外观与位置";
            Native.LocateClock(r => { if (IsVisible && !dragging) Native.PositionClock(this, host.Settings); });
            if (s.HideFullscreen && Native.ForegroundFullscreen(this)) { if (IsVisible) Hide(); return; }
            if (!IsVisible) Show(); Native.PositionClock(this, s);
        }
    }
}
