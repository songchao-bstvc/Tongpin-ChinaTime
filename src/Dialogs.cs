using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Tongpin
{
    public sealed class MemoWindow : Window
    {
        public string SavedDate;
        public MemoWindow(App app, Memo original, DateTime selected, Window owner)
        {
            Title = original == null ? "添加私人备忘" : "编辑私人备忘"; Width = 540; Height = 670; WindowStartupLocation = WindowStartupLocation.Manual;
            Left = owner.Left + (owner.ActualWidth - Width) / 2; Top = owner.Top + (owner.ActualHeight - Height) / 2;
            ResizeMode = ResizeMode.NoResize; ShowInTaskbar = true; Topmost = true; Ui.Style(this);
            var m = original?.Clone() ?? new Memo { Date = China.Key(selected) };
            var p = new StackPanel { Margin = new Thickness(26) }; Content = p;
            p.Children.Add(Ui.Text("把重要的小事，放进中国的日历。", 16, Ui.Accent));
            Ui.Label(p, "标题"); var title = new TextBox { Text = m.Title, MaxLength = 160 }; p.Children.Add(title);
            var dateRow = new Grid(); dateRow.ColumnDefinitions.Add(new ColumnDefinition()); dateRow.ColumnDefinitions.Add(new ColumnDefinition());
            var dp = new StackPanel { Margin = new Thickness(0, 0, 14, 0) }; Ui.Label(dp, "中国日期  YYYY-MM-DD"); var date = new TextBox { Text = m.Date }; dp.Children.Add(date); dateRow.Children.Add(dp);
            var tp = new StackPanel(); Ui.Label(tp, "中国时间  HH:mm（24 小时）"); var time = new TextBox { Text = m.Time }; tp.Children.Add(time); Grid.SetColumn(tp, 1); dateRow.Children.Add(tp); p.Children.Add(dateRow);
            var preview = Ui.Text("", 12, Ui.Green); p.Children.Add(preview);
            Action update = () => { DateTime d; TimeSpan t; if (DateTime.TryParseExact(date.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && TimeSpan.TryParseExact(time.Text, @"hh\:mm", CultureInfo.InvariantCulture, out t) && t < TimeSpan.FromDays(1)) preview.Text = "你的本地时间：" + TimeZoneInfo.ConvertTimeFromUtc(China.ToUtc(d.Add(t)), TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm"); else preview.Text = "请输入完整日期和 24 小时时间。"; };
            date.TextChanged += (s, e) => update(); time.TextChanged += (s, e) => update(); update();
            Ui.Label(p, "重复"); var repeat = new ComboBox { ItemsSource = Schedule.RepeatNames, SelectedIndex = Array.IndexOf(Schedule.RepeatKeys, m.Repeat) }; p.Children.Add(repeat);
            var remind = new CheckBox { Content = "到时提醒我（使用中国时间）", IsChecked = m.Remind }; p.Children.Add(remind);
            Ui.Label(p, "私人备注"); var note = new TextBox { Text = m.Note, Height = 140, MaxLength = 20000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; p.Children.Add(note);
            p.Children.Add(Ui.Text("每月 / 每年重复：不存在的日期会跳过。\n电脑恢复运行后，会补上尚未处理的到期提醒。", 11, Ui.Muted));
            var actions = Ui.Row(Ui.Button("保存备忘", () => {
                m.Title = title.Text.Trim(); m.Note = note.Text; m.Date = date.Text.Trim(); m.Time = time.Text.Trim(); m.Repeat = Schedule.RepeatKeys[Math.Max(0, repeat.SelectedIndex)]; m.Remind = remind.IsChecked == true; m.Modified = DateTime.UtcNow.Ticks;
                var next = Repository.Deserialize(Repository.Serialize(app.Store.Data));
                if (original == null) next.Memos.Add(m); else next.Memos[next.Memos.FindIndex(x => x.Id == m.Id)] = m;
                Schedule.Validate(next);
                bool timingChanged = original == null || original.Date != m.Date || original.Time != m.Time || original.Repeat != m.Repeat || original.Remind != m.Remind;
                if (timingChanged && m.Remind && m.Repeat == "once" && Schedule.At(m, China.ParseDate(m.Date)).Utc < DateTime.UtcNow)
                    if (MessageBox.Show(this, "这个中国时间已经过去。保存后会立即显示提醒。", "已过去的时间", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
                if (timingChanged) { next.Acknowledged.RemoveAll(x => x.StartsWith(m.Id + "|")); foreach (var k in next.Snoozes.Keys.Where(x => x.StartsWith(m.Id + "|")).ToArray()) next.Snoozes.Remove(k); }
                app.Store.Replace(next); SavedDate = m.Date; DialogResult = true;
            }, true), Ui.Button("取消", () => Close())); actions.Margin = new Thickness(0, 20, 0, 0); p.Children.Add(actions);
            Loaded += (s, e) => title.Focus();
        }
    }
    public sealed class ReminderWindow : Window
    {
        public Occurrence Occurrence; public bool IsTest;
        public ReminderWindow(App app, Occurrence occurrence, bool test)
        {
            Occurrence = occurrence; IsTest = test; Title = test ? "同频 · 测试提醒" : "同频 · 中国时间提醒"; Width = 390; Height = 255;
            Ui.Style(this); WindowStyle = WindowStyle.None; ShowInTaskbar = app.DiagnosticWindows; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowActivated = false;
            var p = new StackPanel { Margin = new Thickness(22) }; Content = p;
            p.Children.Add(Ui.Text(test ? "提醒已就位" : "中国的这一刻，有件小事", 13, Ui.Accent));
            var title = Ui.Text(test ? "你和中国，正在同一刻。" : app.Store.Data.Settings.PrivateNotification ? "你有一条私人备忘。" : occurrence.Memo.Title, 20); title.Margin = new Thickness(0, 12, 0, 12); title.MaxHeight = 60; p.Children.Add(title);
            p.Children.Add(Ui.Text("中国 " + (test ? China.Now : occurrence.When).ToString("M月d日 HH:mm") + (test ? " · 测试提醒" : occurrence.When < China.Now.AddMinutes(-2) ? " · 补发提醒" : ""), 12, Ui.Muted));
            var row = Ui.Row(Ui.Button(test ? "测试完成" : "已完成", () => app.HandleReminder(this, "done"), true), Ui.Button("稍后 10 分钟", () => app.HandleReminder(this, "snooze")), Ui.Button("知道了", () => app.HandleReminder(this, "ack"))); row.Margin = new Thickness(0, 18, 0, 0); foreach (Button b in row.Children) { b.FontSize = 11; b.Padding = new Thickness(8, 7, 8, 7); } p.Children.Add(row);
            p.Children.Add(Ui.Button("打开日历", () => { if (!test) app.Calendar.GoTo(occurrence.When.Date); app.Calendar.Reveal(); }));
            SourceInitialized += (s, e) => { if (!app.DiagnosticWindows) Native.SetToolWindow(this); Native.PositionPanel(this, 390, 255); };
            Closing += (s, e) => { app.CurrentReminder = null; };
        }
    }
}
