using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace Tongpin
{
    public sealed class NextReminder
    {
        public Occurrence Occurrence;
        public DateTime DueUtc;
        public bool Snoozed;
    }
    public static class LiveData
    {
        // Calculate in China wall time; persisted snoozes remain UTC instants.
        // A bounded four-year horizon also includes the next Feb 29 anniversary.
        public static NextReminder Next(Data data, DateTime utc)
        {
            var pending = Schedule.Due(data, utc).FirstOrDefault();
            if (pending != null) return new NextReminder { Occurrence = pending, DueUtc = pending.Utc };
            var handled = new HashSet<string>(data.Completed.Concat(data.Acknowledged));
            var china = China.FromUtc(utc);
            NextReminder best = null;
            Action<Occurrence, DateTime, bool> consider = (o, due, snooze) => {
                if (handled.Contains(o.Key) || due < utc) return;
                if (best == null || due < best.DueUtc) best = new NextReminder { Occurrence = o, DueUtc = due, Snoozed = snooze };
            };
            var memos = data.Memos.Where(m => m.Remind && !m.Deleted).ToDictionary(m => m.Id);
            foreach (var pair in data.Snoozes)
            {
                var parts = pair.Key.Split('|'); Memo m;
                if (memos.TryGetValue(parts[0], out m) && pair.Value >= utc.Ticks && pair.Value <= DateTime.MaxValue.Ticks)
                    consider(Schedule.At(m, China.ParseDate(parts[1])), new DateTime(pair.Value, DateTimeKind.Utc), true);
            }
            foreach (var m in memos.Values)
            {
                var start = China.ParseDate(m.Date);
                if (m.Repeat == "once")
                {
                    var o = Schedule.At(m, start);
                    if (!data.Snoozes.ContainsKey(o.Key)) consider(o, o.Utc, false);
                    continue;
                }
                var first = start > china.Date ? start : china.Date;
                foreach (var d in CandidateDates(m, start, first))
                {
                    if (d.Year > 2099 || (best != null && China.ToUtc(d) > best.DueUtc)) break;
                    var o = Schedule.At(m, d);
                    if (o.Utc < utc || handled.Contains(o.Key) || data.Snoozes.ContainsKey(o.Key)) continue;
                    consider(o, o.Utc, false); break;
                }
            }
            return best;
        }
        static IEnumerable<DateTime> CandidateDates(Memo m, DateTime start, DateTime first)
        {
            if (m.Repeat == "yearly")
            {
                for (int y = first.Year; y <= Math.Min(2099, first.Year + 4); y++)
                    if (DateTime.DaysInMonth(y, start.Month) >= start.Day) { var d = new DateTime(y, start.Month, start.Day); if (d >= first) yield return d; }
                yield break;
            }
            if (m.Repeat == "monthly")
            {
                var month = new DateTime(first.Year, first.Month, 1);
                for (int n = 0; n < 49; n++) { var current = month.AddMonths(n); if (current.Year > 2099) yield break; if (DateTime.DaysInMonth(current.Year, current.Month) >= start.Day) { var d = new DateTime(current.Year, current.Month, start.Day); if (d >= first) yield return d; } }
                yield break;
            }
            int step = m.Repeat == "weekly" ? 7 : 1;
            if (step == 7) first = first.AddDays(((int)start.DayOfWeek - (int)first.DayOfWeek + 7) % 7);
            for (int n = 0; n <= 1461; n += step) { var d = first.AddDays(n); if (d.Year > 2099) yield break; if (m.Repeat != "weekdays" || (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)) yield return d; }
        }
        public static string Countdown(NextReminder next, DateTime utc, bool seconds)
        {
            if (next == null) return "暂无待提醒";
            var span = next.DueUtc - utc;
            if (span.TotalSeconds <= 0) return "有待处理提醒";
            if (span.TotalDays >= 1) return (int)span.TotalDays + "天 " + span.Hours + "小时后";
            if (span.TotalHours >= 1) return (int)span.TotalHours + "小时 " + span.Minutes + "分后";
            return seconds ? (int)span.TotalMinutes + "分 " + span.Seconds.ToString("00") + "秒后" : Math.Ceiling(span.TotalMinutes) + "分钟后";
        }
        public static string Together(string value, DateTime china)
        {
            DateTime since;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out since)) return "在设置中记录你们的第一天";
            return china.Date >= since ? "一起走过 " + ((china.Date - since).Days + 1) + " 天" : "距离见面还有 " + (since - china.Date).Days + " 天";
        }
    }
    public static class DisplayLayout
    {
        public static readonly string[] Placements = { "taskbar", "bottom-left", "bottom-center", "bottom-right", "top-left", "top-center", "top-right", "custom" };
        public static readonly string[] Names = { "任务栏右侧 · 自动避让", "桌面左下", "桌面下方居中", "桌面右下", "桌面左上", "桌面上方居中", "桌面右上", "自由拖动" };
        public static System.Drawing.Rectangle Place(System.Drawing.Rectangle area, int width, int height, string mode, double x, double y, int margin)
        {
            width = Math.Max(1, Math.Min(width, area.Width)); height = Math.Max(1, Math.Min(height, area.Height));
            int dx = Math.Max(0, area.Width - width), dy = Math.Max(0, area.Height - height);
            int left = area.Left, top = area.Top;
            if (mode == "custom") { left += (int)(dx * Math.Max(0, Math.Min(1, x))); top += (int)(dy * Math.Max(0, Math.Min(1, y))); }
            else
            {
                left += mode.EndsWith("left") ? Math.Min(margin, dx) : mode.EndsWith("center") ? dx / 2 : Math.Max(0, dx - margin);
                top += mode.StartsWith("top") ? Math.Min(margin, dy) : Math.Max(0, dy - margin);
            }
            return new System.Drawing.Rectangle(left, top, width, height);
        }
    }
}
