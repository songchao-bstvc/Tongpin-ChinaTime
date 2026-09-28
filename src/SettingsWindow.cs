using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Tongpin
{
    public sealed class SettingsWindow : Window
    {
        readonly App app; Settings draft; bool ready, startup, startupOriginal; TextBox anniversary; TextBlock status; int selectedTab; CheckBox positionLock;
        public SettingsWindow(App host, Window owner)
        {
            app = host; draft = app.Store.Data.Settings.Clone(); startup = startupOriginal = Native.StartupEnabled();
            Title = "同频 · 外观与实时信息"; Width = 670; Height = 760; MinHeight = 550; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = true; Topmost = true; Ui.Style(this);
            Build(); app.PreviewSettings = draft;
            Closed += (s, e) => { app.PreviewSettings = null; app.ApplyAppearance(); };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); if (e.Key == Key.F12 && app.DiagnosticWindows) app.CaptureAll(); };
            SourceInitialized += (s, e) => Native.PositionPanel(this, 670, 760);
        }
        void Build()
        {
            ready = false;
            var frame = new Border { CornerRadius = new CornerRadius(14), BorderBrush = Ui.Line, BorderThickness = new Thickness(1) }; Content = frame;
            var root = new Grid { Margin = new Thickness(26, 23, 26, 22) }; frame.Child = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            var top = new DockPanel(); var close = Ui.Button("×", Close); close.Background = System.Windows.Media.Brushes.Transparent; close.Margin = new Thickness(0); close.Padding = new Thickness(10, 2, 10, 2); DockPanel.SetDock(close, Dock.Right); top.Children.Add(close);
            top.Children.Add(Ui.Text("让时间，融入你的桌面", 23)); header.Children.Add(top);
            top.MouseLeftButtonDown += (s, e) => { if (e.OriginalSource is TextBlock) DragMove(); };
            var subtitle = Ui.Text("同频 2.0   ·   改动即时预览，保存后记住你的偏好", 12, Ui.Muted); subtitle.Margin = new Thickness(0, 8, 0, 0); header.Children.Add(subtitle); root.Children.Add(header);
            var tabs = new TabControl(); Grid.SetRow(tabs, 1); root.Children.Add(tabs);
            var appearance = Tab(tabs, "外观");
            Note(appearance, "让任务栏自然透出，只留下时间。鼠标经过时，轻轻回应。");
            var row = Columns(appearance);
            Choice(row.Item1, "窗口主题", new[] { "跟随 Windows", "浅色 · 晴空", "深色 · 夜色" }, new[] { "system", "light", "dark" }, draft.Theme, v => draft.Theme = v);
            Choice(row.Item2, "日历与卡片材质", new[] { "磨砂玻璃", "纯色 · 更清晰" }, new[] { "glass", "solid" }, draft.Material, v => draft.Material = v);
            Choice(appearance, "时钟底色", new[] { "无底色 · 真透明（推荐）", "柔和衬底 · 适合复杂壁纸" }, new[] { "transparent", "soft" }, draft.ClockStyle, v => draft.ClockStyle = v);
            var toggles = Columns(appearance); Check(toggles.Item1, "显示秒数", draft.Seconds, v => draft.Seconds = v); Check(toggles.Item2, "显示中国日期与星期", draft.DateVisible, v => draft.DateVisible = v);
            Slider(appearance, "时钟大小", .8, 2, draft.ClockScale, v => draft.ClockScale = v);
            Slider(appearance, "时钟文字清晰度", .55, 1, draft.ClockOpacity, v => draft.ClockOpacity = v);
            Check(appearance, "轻柔过渡与悬停反馈", draft.Motion, v => draft.Motion = v);
            Note(appearance, "任务栏上的文字始终跟随任务栏深浅色。Windows 关闭透明效果、动画或启用高对比度时，会自动适配。");

            var position = Tab(tabs, "位置");
            Note(position, "任务栏右侧自动避让应用图标；也可以把时间放在桌面的任意角落。");
            var screens = System.Windows.Forms.Screen.AllScreens;
            Choice(position, "时钟与卡片所在显示器", new[] { "自动 · 主显示器" }.Concat(screens.Select((x, i) => "显示器 " + (i + 1) + (x.Primary ? "（主）" : "") + "   " + x.Bounds.Width + " × " + x.Bounds.Height)).ToArray(), new[] { "" }.Concat(screens.Select(x => x.DeviceName)).ToArray(), draft.Monitor, v => draft.Monitor = v);
            Choice(position, "时钟位置", DisplayLayout.Names, DisplayLayout.Placements, draft.Placement, v => { draft.Placement = v; draft.OffsetX = draft.OffsetY = 0; if (v == "custom") { draft.PositionLocked = false; if (positionLock != null) positionLock.IsChecked = false; } });
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 8) };
            foreach (var key in new[] { "top-left", "top-center", "top-right", "bottom-left", "bottom-center", "bottom-right" })
            {
                var mode = key; var label = DisplayLayout.Names[Array.IndexOf(DisplayLayout.Placements, key)].Replace("桌面", "");
                var b = Ui.Button(label, () => { draft.Placement = mode; draft.OffsetX = draft.OffsetY = 0; Preview(); Build(); }); b.Margin = new Thickness(0, 0, 8, 8); if (mode == draft.Placement) b.Background = Ui.Selected; grid.Children.Add(b);
            }
            position.Children.Add(grid);
            positionLock = Check(position, "锁定时钟位置，避免误拖", draft.PositionLocked, v => draft.PositionLocked = v);
            Note(position, "按住 Ctrl 拖动时钟，可直接放到想要的位置。松开后记住位置，屏幕尺寸变化时保持在可见区域。");
            Choice(position, "实时卡片位置", DisplayLayout.Names.Skip(1).ToArray(), DisplayLayout.Placements.Skip(1).ToArray(), draft.LivePlacement, v => draft.LivePlacement = v);
            Note(position, "拖动实时卡片的顶部标题，可自由摆放。桌面预设都位于任务栏上方，不遮挡开始菜单。");

            var live = Tab(tabs, "实时信息");
            Check(live, "常驻显示实时信息卡片", draft.LiveVisible, v => draft.LiveVisible = v);
            Note(live, "时钟保持简洁，更多信息放在一张可移动的卡片里。");
            var liveCols = Columns(live);
            Check(liveCols.Item1, "本地时间与实时中美时差", draft.LiveLocal, v => draft.LiveLocal = v);
            Check(liveCols.Item2, "农历、节气与休班信息", draft.LiveLunar, v => draft.LiveLunar = v);
            Check(liveCols.Item1, "下一条备忘倒计时", draft.LiveNext, v => draft.LiveNext = v);
            Check(liveCols.Item2, "纪念日 / 见面倒数", draft.LiveTogether, v => draft.LiveTogether = v);
            Check(liveCols.Item1, "今日进度与备忘完成数", draft.LiveProgress, v => draft.LiveProgress = v);
            Check(liveCols.Item2, "让卡片保持在最上方", draft.LiveTopmost, v => draft.LiveTopmost = v);
            Check(live, "卡片隐藏私人备忘标题（点击再查看）", draft.LivePrivate, v => draft.LivePrivate = v);
            Slider(live, "卡片文字清晰度", .65, 1, draft.LiveOpacity, v => draft.LiveOpacity = v);
            var caption = Ui.Text("任务栏副行", 16); caption.Margin = new Thickness(0, 14, 0, 4); live.Children.Add(caption);
            Note(live, "以下内容可与日期轮换，每 8 秒一项；默认只显示日期。");
            var compact = Columns(live);
            Check(compact.Item1, "轮换显示农历", draft.CompactLunar, v => draft.CompactLunar = v);
            Check(compact.Item2, "轮换显示本地时间", draft.CompactLocal, v => draft.CompactLocal = v);
            Check(compact.Item1, "轮换显示提醒倒计时", draft.CompactNext, v => draft.CompactNext = v);
            Check(compact.Item2, "轮换显示纪念日", draft.CompactTogether, v => draft.CompactTogether = v);

            var general = Tab(tabs, "通用");
            Check(general, "登录 Windows 后自动显示中国时钟", startup, v => startup = v);
            Check(general, "全屏看视频 / 游戏时隐藏时钟与卡片", draft.HideFullscreen, v => draft.HideFullscreen = v);
            Check(general, "提醒时播放提示音", draft.Sound, v => draft.Sound = v);
            Check(general, "提醒弹窗隐藏私人标题", draft.PrivateNotification, v => draft.PrivateNotification = v);
            Ui.Label(general, "一起的第一天 / 下一次见面  YYYY-MM-DD（可留空）");
            anniversary = new TextBox { Text = draft.TogetherSince, MaxLength = 10 }; general.Children.Add(anniversary);
            anniversary.TextChanged += (s, e) => { DateTime d; var v = anniversary.Text.Trim(); if (v.Length == 0 || DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) { draft.TogetherSince = v; Preview(); } };
            Note(general, "过去的日期显示一起走过多少天，未来的日期显示见面倒数。\n\n备忘在本机由你的 Windows 账户加密。跨电脑迁移请在日历中导出带密码的备份。\n休班数据覆盖至 2026 年；农历、传统节日与节气可继续计算。");
            general.Children.Add(Ui.Row(Ui.Button("打开数据目录", () => System.Diagnostics.Process.Start("explorer.exe", app.Store.Folder)), Ui.Button("测试提醒", () => app.TestReminder())));
            Note(general, "同频 2.0.0  ·  采用 DesktopClock 与 lunar-csharp（MIT）");
            tabs.SelectedIndex = selectedTab;
            tabs.SelectionChanged += (s, e) => { if (ReferenceEquals(e.Source, tabs)) selectedTab = tabs.SelectedIndex; };

            var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; Grid.SetRow(footer, 2); root.Children.Add(footer);
            status = Ui.Text("正在预览 · 取消可恢复保存前的外观", 11, Ui.Muted); status.Margin = new Thickness(0, 0, 0, 13); footer.Children.Add(status);
            var bottom = new DockPanel();
            var actions = Ui.Row(Ui.Button("取消", Close), Ui.Button("保存设置", Save, true)); DockPanel.SetDock(actions, Dock.Right); bottom.Children.Add(actions);
            bottom.Children.Add(Ui.Button("恢复简洁外观", Reset)); footer.Children.Add(bottom);
            ready = true;
        }
        StackPanel Tab(TabControl tabs, string title)
        {
            var p = new StackPanel { Margin = new Thickness(1, 0, 13, 0) };
            tabs.Items.Add(new TabItem { Header = title, Content = new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } }); return p;
        }
        static void Note(StackPanel p, string text) { var t = Ui.Text(text, 12, Ui.Muted); t.LineHeight = 21; t.Margin = new Thickness(0, 5, 0, 14); p.Children.Add(t); }
        Tuple<StackPanel, StackPanel> Columns(StackPanel parent)
        {
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition());
            var a = new StackPanel { Margin = new Thickness(0, 0, 18, 0) }; var b = new StackPanel(); Grid.SetColumn(b, 1); g.Children.Add(a); g.Children.Add(b); parent.Children.Add(g); return Tuple.Create(a, b);
        }
        void Choice(StackPanel p, string caption, string[] labels, string[] values, string current, Action<string> change)
        {
            Ui.Label(p, caption); var c = new ComboBox { ItemsSource = labels, SelectedIndex = Math.Max(0, Array.IndexOf(values, current)) }; p.Children.Add(c);
            c.SelectionChanged += (s, e) => { if (!ready || c.SelectedIndex < 0) return; change(values[c.SelectedIndex]); Preview(); };
        }
        CheckBox Check(StackPanel p, string caption, bool current, Action<bool> change)
        {
            var c = new CheckBox { Content = caption, IsChecked = current }; p.Children.Add(c);
            RoutedEventHandler set = (s, e) => { if (!ready) return; change(c.IsChecked == true); Preview(); }; c.Checked += set; c.Unchecked += set;
            return c;
        }
        void Slider(StackPanel p, string label, double min, double max, double value, Action<double> change)
        {
            var heading = new DockPanel { Margin = new Thickness(0, 12, 0, 10) }; var number = Ui.Text(Math.Round(value * 100) + "%", 12, Ui.Accent); DockPanel.SetDock(number, Dock.Right); heading.Children.Add(number); heading.Children.Add(Ui.Text(label, 12)); p.Children.Add(heading);
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, SmallChange = .05, LargeChange = .1, TickFrequency = .05, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 9) }; p.Children.Add(slider);
            slider.ValueChanged += (s, e) => { number.Text = Math.Round(slider.Value * 100) + "%"; if (ready) { change(Math.Round(slider.Value, 2)); Preview(); } };
        }
        void Preview() { if (!ready) return; app.PreviewSettings = draft; app.ApplyAppearance(); }
        void Reset()
        {
            var fresh = Settings.Minimal(draft);
            draft = fresh; app.PreviewSettings = draft; app.ApplyAppearance(); Build();
        }
        void Save()
        {
            var value = anniversary.Text.Trim(); DateTime d;
            if (value.Length > 0 && !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) throw new ArgumentException("纪念日期请用 YYYY-MM-DD 格式。");
            draft.TogetherSince = value;
            var before = app.Store.Data.Settings; app.Store.Data.Settings = draft.Clone();
            try { app.Store.Save(); } catch { app.Store.Data.Settings = before; throw; }
            if (startup != startupOriginal) Native.SetStartup(startup);
            app.PreviewSettings = null; app.ApplyAppearance(); Close();
        }
    }
}
