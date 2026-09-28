using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Lunar;
using Lunar.Util;

namespace Tongpin
{
    public static class China
    {
        public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("zh-CN");
        public static DateTime Now { get { return FromUtc(DateTime.UtcNow); } }
        public static DateTime FromUtc(DateTime utc) { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone); }
        public static DateTime ToUtc(DateTime china) { return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(china, DateTimeKind.Unspecified), Zone); }
        public static string Key(DateTime date) { return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static string Week(DateTime d) { return "星期" + "日一二三四五六"[(int)d.DayOfWeek]; }
        public static DateTime ParseDate(string s) { return DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static string LunarText(DateTime d)
        {
            var l = Solar.FromDate(d).Lunar;
            return "农历" + l.MonthInChinese + "月" + l.DayInChinese;
        }
        public static string DayLabel(DateTime d)
        {
            var s = Solar.FromDate(d); var l = s.Lunar;
            if (l.Festivals.Count > 0) return l.Festivals[0];
            if (s.Festivals.Count > 0) return s.Festivals[0];
            if (!string.IsNullOrEmpty(l.JieQi)) return l.JieQi;
            return l.Day == 1 ? l.MonthInChinese + "月" : l.DayInChinese;
        }
        public static string Details(DateTime d)
        {
            var s = Solar.FromDate(d); var l = s.Lunar;
            var words = new List<string> { LunarText(d) };
            words.AddRange(l.Festivals); words.AddRange(s.Festivals);
            if (!string.IsNullOrEmpty(l.JieQi)) words.Add(l.JieQi);
            var h = HolidayUtil.GetHoliday(d.Year, d.Month, d.Day);
            if (h != null) words.Add(h.Name + (h.Work ? " · 调休上班" : " · 放假"));
            return string.Join("  ·  ", words.Distinct());
        }
    }
    public sealed class Memo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Date { get; set; } = China.Key(China.Now);
        public string Time { get; set; } = "20:00";
        public string Title { get; set; } = "";
        public string Note { get; set; } = "";
        public string Repeat { get; set; } = "once";
        public bool Remind { get; set; } = true;
        public bool Deleted { get; set; }
        public long Modified { get; set; } = DateTime.UtcNow.Ticks;
        public Memo Clone() { return (Memo)MemberwiseClone(); }
    }
    public sealed class Settings
    {
        public bool Seconds { get; set; } = false;
        public bool HideFullscreen { get; set; } = true;
        public bool Sound { get; set; } = true;
        public bool PrivateNotification { get; set; } = false;
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public string TogetherSince { get; set; } = "";
        public string Theme { get; set; } = "system";
        public string Material { get; set; } = "glass";
        public string ClockStyle { get; set; } = "transparent";
        public string Placement { get; set; } = "taskbar";
        public string Monitor { get; set; } = "";
        public double ClockScale { get; set; } = 1;
        public double ClockOpacity { get; set; } = 1;
        public double CustomX { get; set; } = .92;
        public double CustomY { get; set; } = .92;
        public bool PositionLocked { get; set; } = true;
        public bool DateVisible { get; set; } = true;
        public bool CompactLunar { get; set; }
        public bool CompactLocal { get; set; }
        public bool CompactNext { get; set; }
        public bool CompactTogether { get; set; }
        public bool Motion { get; set; } = true;
        public bool LiveVisible { get; set; }
        public bool LiveLocal { get; set; } = true;
        public bool LiveLunar { get; set; } = true;
        public bool LiveNext { get; set; } = true;
        public bool LiveTogether { get; set; } = true;
        public bool LiveProgress { get; set; } = true;
        public bool LiveTopmost { get; set; } = true;
        public bool LivePrivate { get; set; } = true;
        public double LiveOpacity { get; set; } = 1;
        public string LivePlacement { get; set; } = "bottom-right";
        public double LiveX { get; set; } = .92;
        public double LiveY { get; set; } = .70;
        public Settings Clone() { return (Settings)MemberwiseClone(); }
        public static Settings Minimal(Settings old) { return new Settings { Sound = old.Sound, HideFullscreen = old.HideFullscreen, PrivateNotification = old.PrivateNotification, TogetherSince = old.TogetherSince }; }
    }
    public sealed class Data
    {
        public int Version { get; set; } = 1;
        public List<Memo> Memos { get; set; } = new List<Memo>();
        public List<string> Completed { get; set; } = new List<string>();
        public List<string> Acknowledged { get; set; } = new List<string>();
        public Dictionary<string, long> Snoozes { get; set; } = new Dictionary<string, long>();
        public Settings Settings { get; set; } = new Settings();
    }
    public sealed class Occurrence
    {
        public Memo Memo; public DateTime When;
        public string Key { get { return Memo.Id + "|" + China.Key(When); } }
        public DateTime Utc { get { return China.ToUtc(When); } }
    }
    public static class Schedule
    {
        public static readonly string[] RepeatKeys = { "once", "daily", "weekdays", "weekly", "monthly", "yearly" };
        public static readonly string[] RepeatNames = { "不重复", "每天", "每周一至周五", "每周", "每月同日", "每年同日（公历）" };
        public static bool OnDate(Memo m, DateTime date)
        {
            if (m.Deleted) return false;
            var start = China.ParseDate(m.Date); date = date.Date;
            if (date < start) return false;
            switch (m.Repeat)
            {
                case "daily": return true;
                case "weekdays": return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
                case "weekly": return date.DayOfWeek == start.DayOfWeek;
                case "monthly": return date.Day == start.Day;
                case "yearly": return date.Day == start.Day && date.Month == start.Month;
                default: return date == start;
            }
        }
        public static Occurrence At(Memo m, DateTime d) { return new Occurrence { Memo = m, When = d.Date.Add(TimeSpan.ParseExact(m.Time, @"hh\:mm", CultureInfo.InvariantCulture)) }; }
        public static List<Occurrence> Day(Data data, DateTime date)
        { return data.Memos.Where(m => OnDate(m, date)).Select(m => At(m, date)).OrderBy(o => o.When).ToList(); }
        public static List<Occurrence> Due(Data data, DateTime utc)
        {
            var now = China.FromUtc(utc); var due = new List<Occurrence>();
            foreach (var m in data.Memos.Where(x => x.Remind && !x.Deleted))
            {
                if (m.Repeat == "once") { Consider(data, At(m, China.ParseDate(m.Date)), utc, due); continue; }
                // Coalesce old repeating alerts to the most recent occurrence within seven days.
                // Once today's occurrence is handled, do not resurrect older repeats.
                for (int day = 0; day < 7; day++)
                {
                    var d = now.Date.AddDays(-day);
                    if (!OnDate(m, d)) continue;
                    var o = At(m, d);
                    if (o.Utc > utc) continue;
                    Consider(data, o, utc, due); break;
                }
            }
            // A snoozed occurrence remains due even across midnight or a later repeat.
            foreach (var pair in data.Snoozes.Where(x => x.Value <= utc.Ticks))
            {
                var p = pair.Key.Split('|'); var m = data.Memos.FirstOrDefault(x => x.Id == p[0]);
                if (m == null || m.Deleted || !m.Remind) continue;
                Consider(data, At(m, China.ParseDate(p[1])), utc, due);
            }
            return due.GroupBy(x => x.Key).Select(g => g.First()).OrderBy(x => x.Utc).ToList();
        }
        static void Consider(Data data, Occurrence o, DateTime utc, List<Occurrence> list)
        {
            if (o.Utc > utc || data.Completed.Contains(o.Key) || data.Acknowledged.Contains(o.Key)) return;
            long until; if (data.Snoozes.TryGetValue(o.Key, out until) && until > utc.Ticks) return;
            list.Add(o);
        }
        public static void Validate(Data d)
        {
            if (d == null || d.Version != 1 || d.Memos == null || d.Completed == null || d.Acknowledged == null || d.Snoozes == null || d.Settings == null)
                throw new InvalidDataException("备份格式或版本不受支持。");
            if (d.Memos.Count > 20000) throw new InvalidDataException("备忘数量超过 20000 条。");
            var ids = new HashSet<string>();
            foreach (var m in d.Memos)
            {
                Guid id; TimeSpan time; DateTime date;
                if (!Guid.TryParseExact(m.Id, "N", out id) || !ids.Add(m.Id) || !DateTime.TryParseExact(m.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                    || date.Year < 1901 || date.Year > 2099 || !TimeSpan.TryParseExact(m.Time, @"hh\:mm", CultureInfo.InvariantCulture, out time)
                    || time >= TimeSpan.FromDays(1) || !RepeatKeys.Contains(m.Repeat) || string.IsNullOrWhiteSpace(m.Title) || m.Title.Length > 160 || (m.Note ?? "").Length > 20000)
                    throw new InvalidDataException("备忘包含无效的日期、时间或内容。");
            }
            foreach (var key in d.Snoozes.Keys.Concat(d.Completed).Concat(d.Acknowledged))
            {
                var parts = key.Split('|'); Guid id; DateTime date;
                if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out id) || !DateTime.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    throw new InvalidDataException("提醒状态格式无效。");
            }
            if (Math.Abs((long)d.Settings.OffsetX) > 1000 || Math.Abs((long)d.Settings.OffsetY) > 1000) throw new InvalidDataException("时钟位置超出范围。");
            var s = d.Settings;
            if (!new[] { "system", "light", "dark" }.Contains(s.Theme) || !new[] { "glass", "solid" }.Contains(s.Material)
                || !new[] { "transparent", "soft" }.Contains(s.ClockStyle) || !DisplayLayout.Placements.Contains(s.Placement)
                || !DisplayLayout.Placements.Contains(s.LivePlacement) || s.LivePlacement == "taskbar"
                || !FiniteRange(s.ClockScale, .8, 2) || !FiniteRange(s.ClockOpacity, .55, 1) || !FiniteRange(s.LiveOpacity, .65, 1)
                || !FiniteRange(s.CustomX, 0, 1) || !FiniteRange(s.CustomY, 0, 1) || !FiniteRange(s.LiveX, 0, 1) || !FiniteRange(s.LiveY, 0, 1))
                throw new InvalidDataException("显示设置超出范围。");
        }
        static bool FiniteRange(double n, double low, double high) { return !double.IsNaN(n) && !double.IsInfinity(n) && n >= low && n <= high; }
    }
    public sealed class Repository
    {
        public readonly string Folder;
        public Data Data { get; private set; }
        public string PathName { get { return Path.Combine(Folder, "private-memos.bin"); } }
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Tongpin.ChinaTime.private.v1");
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 24000000 };
        public Repository(string folder)
        {
            Folder = folder; Directory.CreateDirectory(Folder);
            Data = File.Exists(PathName) ? Decode(File.ReadAllBytes(PathName)) : new Data();
            Schedule.Validate(Data);
        }
        public static byte[] Serialize(Data data) { return Encoding.UTF8.GetBytes(Json.Serialize(data)); }
        public static Data Deserialize(byte[] bytes) { var d = Json.Deserialize<Data>(Encoding.UTF8.GetString(bytes)); Schedule.Validate(d); return d; }
        public static Data Decode(byte[] bytes) { return Deserialize(ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser)); }
        public void Replace(Data data) { Schedule.Validate(data); var old = Data; Data = data; try { Save(); } catch { Data = old; throw; } }
        public void Save()
        {
            Schedule.Validate(Data);
            var bytes = ProtectedData.Protect(Serialize(Data), Entropy, DataProtectionScope.CurrentUser);
            var temp = PathName + ".tmp-" + Guid.NewGuid().ToString("N");
            using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(bytes, 0, bytes.Length); f.Flush(true); }
            if (File.Exists(PathName))
            {
                var backups = Path.Combine(Folder, "backups"); Directory.CreateDirectory(backups);
                var daily = Path.Combine(backups, "memos-" + China.Key(China.Now) + ".bin");
                if (!File.Exists(daily)) File.Copy(PathName, daily);
                File.Replace(temp, PathName, PathName + ".previous", true);
            }
            else File.Move(temp, PathName);
        }
        public int Merge(Data incoming)
        {
            Schedule.Validate(incoming);
            var merged = Deserialize(Serialize(Data)); int changed = 0;
            foreach (var m in incoming.Memos)
            {
                var index = merged.Memos.FindIndex(x => x.Id == m.Id);
                if (index < 0) { merged.Memos.Add(m); changed++; }
                else if (m.Modified > merged.Memos[index].Modified) { merged.Memos[index] = m; changed++; }
            }
            merged.Completed = merged.Completed.Union(incoming.Completed).ToList();
            merged.Acknowledged = merged.Acknowledged.Union(incoming.Acknowledged).ToList();
            foreach (var s in incoming.Snoozes) if (!merged.Snoozes.ContainsKey(s.Key)) merged.Snoozes[s.Key] = s.Value;
            Replace(merged); return changed;
        }
    }
    public static class PortableBackup
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("CSTBACK1");
        public static byte[] Encrypt(byte[] plain, string password)
        {
            if (password.Length < 8) throw new ArgumentException("备份密码至少 8 个字符。");
            byte[] salt = new byte[16], iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); rng.GetBytes(iv); }
            byte[] keys;
            using (var kdf = new Rfc2898DeriveBytes(password, salt, 150000, HashAlgorithmName.SHA256)) keys = kdf.GetBytes(64);
            byte[] cipher;
            using (var aes = Aes.Create()) { aes.Key = keys.Take(32).ToArray(); aes.IV = iv; using (var tr = aes.CreateEncryptor()) cipher = tr.TransformFinalBlock(plain, 0, plain.Length); }
            var payload = Magic.Concat(salt).Concat(iv).Concat(cipher).ToArray();
            using (var mac = new HMACSHA256(keys.Skip(32).ToArray())) return payload.Concat(mac.ComputeHash(payload)).ToArray();
        }
        public static byte[] Decrypt(byte[] bytes, string password)
        {
            if (bytes.Length < 88 || bytes.Length > 30000000 || !bytes.Take(8).SequenceEqual(Magic)) throw new InvalidDataException("不是同频的加密备份。");
            var salt = bytes.Skip(8).Take(16).ToArray(); var iv = bytes.Skip(24).Take(16).ToArray(); byte[] keys;
            using (var kdf = new Rfc2898DeriveBytes(password, salt, 150000, HashAlgorithmName.SHA256)) keys = kdf.GetBytes(64);
            var payload = bytes.Take(bytes.Length - 32).ToArray(); byte[] tag;
            using (var mac = new HMACSHA256(keys.Skip(32).ToArray())) tag = mac.ComputeHash(payload);
            int diff = 0; for (int i = 0; i < 32; i++) diff |= tag[i] ^ bytes[bytes.Length - 32 + i];
            if (diff != 0) throw new CryptographicException("密码不正确，或备份文件已损坏。");
            var cipher = payload.Skip(40).ToArray();
            using (var aes = Aes.Create()) { aes.Key = keys.Take(32).ToArray(); aes.IV = iv; using (var tr = aes.CreateDecryptor()) return tr.TransformFinalBlock(cipher, 0, cipher.Length); }
        }
    }
}
