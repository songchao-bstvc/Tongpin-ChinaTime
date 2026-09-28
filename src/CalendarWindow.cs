using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DesktopClock;
using Lunar.Util;

namespace Tongpin
{
    public sealed class CalendarWindow : Window
    {
        readonly App host; readonly TextBlock chinaTime, localTime, dateHeader, monthTitle, selectedTitle, detail, footer;
        readonly UniformGrid calendar; readonly StackPanel memoList; readonly TextBox search;
        DateTime selected = China.Now.Date, month = new DateTime(China.Now.Year, China.Now.Month, 1), lastToday = China.Now.Date;
        bool upcoming, recycle;
        public CalendarWindow(App app)
        {
            host = app; Title = "同频 · 中国日历与私人备忘"; Width = 810; Height = 750; MinWidth = 700; MinHeight = 600;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = true; Topmost = true;
            Ui.Style(this);
            var root = new Grid { Margin = new Thickness(24) }; Content = new Border { Child = root, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var title = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
            var close = Ui.Button("收起  ×", () => Hide()); DockPanel.SetDock(close, Dock.Right); title.Children.Add(close);
            title.Children.Add(Ui.Text("同频   /   此刻，在一起", 18, Ui.Accent)); root.Children.Add(title);
            title.MouseLeftButtonDown += (s, e) => { if (e.OriginalSource is TextBlock) DragMove(); };
            var hero = new Grid(); hero.ColumnDefinitions.Add(new ColumnDefinition()); hero.ColumnDefinitions.Add(new ColumnDefinition());
            var cn = new StackPanel(); cn.Children.Add(Ui.Text("中国标准时间   UTC+08:00", 12, Ui.Accent)); chinaTime = Ui.Text("", 44); cn.Children.Add(chinaTime);
            dateHeader = Ui.Text("", 13, Ui.Muted); cn.Children.Add(dateHeader); hero.Children.Add(cn);
            var us = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) }; Grid.SetColumn(us, 1);
            us.Children.Add(Ui.Text("你所在的时区", 12, Ui.Muted)); localTime = Ui.Text("", 17); us.Children.Add(localTime);
            detail = Ui.Text("", 12, Ui.Green); detail.Margin = new Thickness(0, 9, 0, 0); us.Children.Add(detail); hero.Children.Add(us);
            var heroCard = Ui.Card(hero, new Thickness(20)); heroCard.Margin = new Thickness(0, 0, 0, 20); Grid.SetRow(heroCard, 1); root.Children.Add(heroCard);
            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); Grid.SetRow(body, 2); root.Children.Add(body);
            var left = new StackPanel { Margin = new Thickness(0, 0, 20, 0) }; body.Children.Add(left);
            var nav = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var arrows = Ui.Row(Ui.Button("‹", () => MoveMonth(-1)), Ui.Button("›", () => MoveMonth(1)), Ui.Button("今天", () => { selected = China.Now.Date; month = new DateTime(selected.Year, selected.Month, 1); upcoming = recycle = false; search.Text = ""; Refresh(); }));
            DockPanel.SetDock(arrows, Dock.Right); nav.Children.Add(arrows); monthTitle = Ui.Text("", 18); monthTitle.VerticalAlignment = VerticalAlignment.Center; nav.Children.Add(monthTitle); left.Children.Add(nav);
            var days = new UniformGrid { Columns = 7, Margin = new Thickness(0, 4, 0, 8) };
            foreach (var day in new[] { "一", "二", "三", "四", "五", "六", "日" }) days.Children.Add(new TextBlock { Text = day, HorizontalAlignment = HorizontalAlignment.Center, Foreground = day == "六" || day == "日" ? Ui.Accent : Ui.Muted });
            left.Children.Add(days); calendar = new UniformGrid { Columns = 7, Rows = 6 }; left.Children.Add(calendar);
            var hint = Ui.Text("● 有备忘     休 / 班：放假与调休", 11, Ui.Muted); hint.Margin = new Thickness(2, 8, 0, 0); left.Children.Add(hint);
            var right = new Grid(); Grid.SetColumn(right, 1); body.Children.Add(right);
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var heading = new DockPanel(); var add = Ui.Button("＋ 备忘", () => Edit(null), true); DockPanel.SetDock(add, Dock.Right); heading.Children.Add(add); selectedTitle = Ui.Text("", 17); selectedTitle.VerticalAlignment = VerticalAlignment.Center; heading.Children.Add(selectedTitle); right.Children.Add(heading);
            search = new TextBox { ToolTip = "搜索全部日期的标题和正文", Height = 36, Margin = new Thickness(0, 10, 8, 10) }; search.TextChanged += (s, e) => RefreshList(); Grid.SetRow(search, 1); right.Children.Add(search);
            var tabs = Ui.Row(Ui.Button("当日", () => { upcoming = recycle = false; search.Text = ""; RefreshList(); }), Ui.Button("未来 7 天", () => { upcoming = true; recycle = false; search.Text = ""; RefreshList(); }), Ui.Button("回收站", () => { recycle = true; upcoming = false; search.Text = ""; RefreshList(); })); Grid.SetRow(tabs, 2); tabs.Margin = new Thickness(0, 0, 0, 12); right.Children.Add(tabs);
            memoList = new StackPanel(); var scroll = new ScrollViewer { Content = memoList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 3); right.Children.Add(scroll);
            var bottom = new StackPanel { Margin = new Thickness(0, 18, 0, 0) }; Grid.SetRow(bottom, 3); root.Children.Add(bottom);
            footer = Ui.Text("", 11, Ui.Muted); footer.Margin = new Thickness(0, 0, 0, 12); bottom.Children.Add(footer);
            bottom.Children.Add(Ui.Row(Ui.Button("外观与设置", () => host.OpenSettings()), Ui.Button("实时卡片", () => host.ToggleLive()), Ui.Button("导出加密备份", Export), Ui.Button("恢复备份", Import), Ui.Button("测试提醒", () => host.TestReminder())));
            SourceInitialized += (s, e) => Native.PositionPanel(this, Width, Height);
            Closing += (s, e) => { if (!host.Quitting) { e.Cancel = true; Hide(); } };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Hide(); if (e.Key == Key.F12 && app.DiagnosticWindows) app.CaptureAll(); if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) Edit(null); if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) search.Focus(); };
            Refresh(); Tick();
        }
        void MoveMonth(int n) { var target = month.AddMonths(n); if (target.Year < 1901 || target.Year > 2099) return; month = target; RefreshCalendar(); }
        public void Reveal() { bool hidden = !IsVisible; Refresh(); Tick(); Show(); Native.Decorate(this); Native.PositionPanel(this, 810, 750); Activate(); if (hidden) Theme.FadeIn(this, host.Settings); }
        public void GoTo(DateTime day) { selected = day.Date; month = new DateTime(day.Year, day.Month, 1); upcoming = recycle = false; search.Text = ""; Refresh(); }
        public void Tick()
        {
            var now = China.Now; var local = DateTime.Now;
            chinaTime.Text = now.ToString(host.Settings.Seconds ? "HH:mm:ss" : "HH:mm");
            dateHeader.Text = now.ToString("yyyy年M月d日") + "  " + China.Week(now);
            var offset = (China.Zone.GetUtcOffset(DateTime.UtcNow) - TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)).TotalHours;
            localTime.Text = local.ToString("MM月dd日 HH:mm") + "\n" + (TimeZoneInfo.Local.IsDaylightSavingTime(local) ? TimeZoneInfo.Local.DaylightName : TimeZoneInfo.Local.StandardName);
            detail.Text = "中国比本地" + (offset >= 0 ? "快 " : "慢 ") + Math.Abs(offset).ToString("0.#") + " 小时" + (now.Date > local.Date ? " · 已是明天" : now.Date < local.Date ? " · 仍是昨天" : "");
            var anniversary = host.Settings.TogetherSince; DateTime since;
            if (DateTime.TryParseExact(anniversary, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out since)) detail.Text += "\n" + (now.Date >= since ? "一起走过 " + ((now.Date - since).Days + 1) + " 天" : "距离约定还有 " + (since - now.Date).Days + " 天");
            if (now.Date != lastToday) { if (selected == lastToday) { selected = now.Date; month = new DateTime(selected.Year, selected.Month, 1); } lastToday = now.Date; Refresh(); }
        }
        public void Refresh() { RefreshCalendar(); RefreshList(); }
        void RefreshCalendar()
        {
            calendar.Children.Clear(); monthTitle.Text = month.ToString("yyyy年 M月");
            var start = month.AddDays(-(((int)month.DayOfWeek + 6) % 7));
            for (int i = 0; i < 42; i++)
            {
                var d = start.AddDays(i); var today = d == China.Now.Date; var active = d == selected;
                var p = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
                var h = HolidayUtil.GetHoliday(d.Year, d.Month, d.Day);
                var num = new TextBlock { Text = d.Day.ToString() + (h == null ? "" : h.Work ? "班" : "休"), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Foreground = today ? Ui.Accent : d.Month != month.Month ? Ui.Faint : Ui.Ink };
                if (Schedule.Day(host.Store.Data, d).Any()) num.Text += "·";
                p.Children.Add(num); p.Children.Add(new TextBlock { Text = China.DayLabel(d), FontSize = 10, Height = 16, LineHeight = 15, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, HorizontalAlignment = HorizontalAlignment.Center, Foreground = d.Month == month.Month ? Ui.Muted : Ui.Faint, TextTrimming = TextTrimming.CharacterEllipsis });
                var b = new Button { Content = p, Padding = new Thickness(1, 4, 1, 2), Margin = new Thickness(1), Height = 49, Background = active ? Ui.Selected : today ? Ui.Control : Brushes.Transparent, ToolTip = d.ToString("yyyy-MM-dd") + "  " + China.Details(d) };
                b.Click += (s, e) => { selected = d; upcoming = recycle = false; search.Text = ""; Refresh(); };
                calendar.Children.Add(b);
            }
        }
        void RefreshList()
        {
            if (memoList == null) return;
            memoList.Children.Clear(); var data = host.Store.Data; var query = (search?.Text ?? "").Trim();
            selectedTitle.Text = recycle ? "回收站" : query.Length > 0 ? "搜索结果" : upcoming ? "未来 7 天" : selected.ToString("M月d日") + " · 备忘";
            footer.Text = China.Details(selected) + "   |   第 " + selected.GetIsoWeekOfYear() + " 周" + (selected.Year > 2026 ? "   |   此年份休班数据尚未更新" : "");
            if (recycle)
            {
                foreach (var m in data.Memos.Where(x => x.Deleted))
                { var row = new StackPanel(); row.Children.Add(Ui.Text(m.Title)); row.Children.Add(Ui.Button("恢复这条备忘", () => { m.Deleted = false; m.Modified = DateTime.UtcNow.Ticks; host.SaveAndRefresh(); })); var card = Ui.Card(row, new Thickness(12)); card.Margin = new Thickness(0, 0, 8, 10); memoList.Children.Add(card); }
            }
            else if (query.Length > 0)
            {
                foreach (var m in data.Memos.Where(x => !x.Deleted && (x.Title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || (x.Note ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)).Take(150)) AddMemo(Schedule.At(m, China.ParseDate(m.Date)), true);
            }
            else if (upcoming) { for (int d = 0; d < 7; d++) foreach (var o in Schedule.Day(data, China.Now.Date.AddDays(d))) AddMemo(o, true); }
            else foreach (var o in Schedule.Day(data, selected)) AddMemo(o, false);
            if (memoList.Children.Count == 0)
            { var empty = Ui.Text(recycle ? "回收站是空的。" : query.Length > 0 ? "没有找到匹配的备忘。" : "把想记住的小事，留在这一天。\n\n点「＋ 备忘」记录约定、问候，\n或只属于你的日常。", 14, Ui.Muted); empty.Margin = new Thickness(12, 26, 12, 0); memoList.Children.Add(empty); }
        }
        void AddMemo(Occurrence o, bool withDate)
        {
            var m = o.Memo; var done = host.Store.Data.Completed.Contains(o.Key); var p = new StackPanel();
            var text = Ui.Text(m.Title, 14, done ? Ui.Muted : Ui.Ink); text.FontWeight = FontWeights.SemiBold; if (done) text.TextDecorations = TextDecorations.Strikethrough; p.Children.Add(text);
            p.Children.Add(new TextBlock { Text = (withDate ? o.When.ToString("M/d ") : "") + (m.Remind ? "中国 " + m.Time : "仅备忘") + (m.Repeat != "once" ? " · " + Schedule.RepeatNames[Array.IndexOf(Schedule.RepeatKeys, m.Repeat)] : ""), Foreground = Ui.Accent, FontSize = 11, Margin = new Thickness(0, 7, 0, 6) });
            if (!string.IsNullOrWhiteSpace(m.Note)) { var note = Ui.Text(m.Note, 12, Ui.Muted); note.MaxHeight = 70; note.TextTrimming = TextTrimming.CharacterEllipsis; p.Children.Add(note); }
            var actions = Ui.Row(Ui.Button(done ? "撤销完成" : "完成", () => { if (done) host.Store.Data.Completed.Remove(o.Key); else { host.Store.Data.Completed.Add(o.Key); host.Store.Data.Snoozes.Remove(o.Key); } host.SaveAndRefresh(); }), Ui.Button("编辑", () => Edit(m)), Ui.Button("移入回收站", () => { m.Deleted = true; m.Modified = DateTime.UtcNow.Ticks; host.SaveAndRefresh(); })); actions.Margin = new Thickness(0, 10, 0, 0);
            foreach (Button b in actions.Children) { b.FontSize = 10; b.Padding = new Thickness(8, 6, 8, 6); b.Margin = new Thickness(0, 0, 5, 0); } p.Children.Add(actions);
            var card = Ui.Card(p, new Thickness(13)); card.Margin = new Thickness(0, 0, 8, 10); memoList.Children.Add(card);
        }
        void Edit(Memo original)
        { var editor = new MemoWindow(host, original, selected, this); if (editor.ShowDialog() == true) { selected = China.ParseDate(editor.SavedDate); month = new DateTime(selected.Year, selected.Month, 1); upcoming = recycle = false; search.Text = ""; Refresh(); host.RefreshLive(); host.ScanReminders(); } }
        void Export()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "导出带密码的备份", Filter = "同频加密备份|*.cstbackup", FileName = "同频备忘-" + China.Key(China.Now) + ".cstbackup" };
            if (dialog.ShowDialog(this) != true) return;
            var password = Ui.Password(this, true); if (password == null) return;
            File.WriteAllBytes(dialog.FileName, PortableBackup.Encrypt(Repository.Serialize(host.Store.Data), password));
            MessageBox.Show(this, "加密备份已导出。请把文件和密码分开保管。", "备份完成");
        }
        void Import()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "恢复备忘（合并，保留现有内容）", Filter = "同频备份|*.cstbackup;*.bin;*.previous", InitialDirectory = Path.Combine(host.Store.Folder, "backups") };
            if (dialog.ShowDialog(this) != true) return;
            if (new FileInfo(dialog.FileName).Length > 30000000) throw new InvalidDataException("备份文件过大。");
            var bytes = File.ReadAllBytes(dialog.FileName); Data incoming;
            if (dialog.FileName.EndsWith(".cstbackup", StringComparison.OrdinalIgnoreCase)) { var password = Ui.Password(this, false); if (password == null) return; incoming = Repository.Deserialize(PortableBackup.Decrypt(bytes, password)); }
            else incoming = Repository.Decode(bytes);
            if (MessageBox.Show(this, "备份中有 " + incoming.Memos.Count + " 条记录。将与现有备忘合并；同一条记录保留较新版本。", "恢复备份", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            var count = host.Store.Merge(incoming); Refresh(); host.RefreshLive(); MessageBox.Show(this, "已恢复或更新 " + count + " 条备忘。", "恢复完成");
        }
    }
}
