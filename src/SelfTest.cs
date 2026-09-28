using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Lunar;
using DesktopClock;

namespace Tongpin
{
    public static class SelfTest
    {
        static readonly List<object> Results = new List<object>();
        static void Check(string name, Func<bool> run) { try { bool pass = run(); Results.Add(new { name, passed = pass, error = "" }); } catch (Exception e) { Results.Add(new { name, passed = false, error = e.ToString() }); } }
        public static int Run(string folder)
        {
            Directory.CreateDirectory(folder);
            Check("UTC+8 and Chinese next-day boundary", () => China.FromUtc(new DateTime(2026, 9, 28, 16, 0, 0, DateTimeKind.Utc)) == new DateTime(2026, 9, 29));
            Check("US DST changes do not move China time", () => {
                var east = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                var before = new DateTime(2026, 11, 1, 5, 59, 0, DateTimeKind.Utc); var after = before.AddMinutes(2);
                return China.FromUtc(after) - China.FromUtc(before) == TimeSpan.FromMinutes(2) && TimeZoneInfo.ConvertTimeFromUtc(after, east) - TimeZoneInfo.ConvertTimeFromUtc(before, east) == TimeSpan.FromMinutes(-58);
            });
            Check("Spring Festival 2026 lunar module", () => Solar.FromDate(new DateTime(2026, 2, 17)).Lunar.Month == 1 && Solar.FromDate(new DateTime(2026, 2, 17)).Lunar.Day == 1);
            Check("Mid-Autumn 2026 lunar module", () => Solar.FromDate(new DateTime(2026, 9, 25)).Lunar.Festivals.Contains("中秋节"));
            Check("DesktopClock ISO week boundary", () => new DateTime(2021, 1, 1).GetIsoWeekOfYear() == 53);
            Check("Monthly 31st skips short months", () => { var m = new Memo { Date = "2026-01-31", Title = "test", Repeat = "monthly" }; return !Schedule.OnDate(m, new DateTime(2026, 2, 28)) && Schedule.OnDate(m, new DateTime(2026, 3, 31)); });
            Check("Weekdays exclude Sunday", () => { var m = new Memo { Date = "2026-09-01", Title = "test", Repeat = "weekdays" }; return !Schedule.OnDate(m, new DateTime(2026, 9, 27)) && Schedule.OnDate(m, new DateTime(2026, 9, 28)); });
            Check("Feb 29 annual repeat skips nonleap years", () => { var m = new Memo { Date = "2024-02-29", Title = "test", Repeat = "yearly" }; return !Schedule.OnDate(m, new DateTime(2025, 2, 28)) && Schedule.OnDate(m, new DateTime(2028, 2, 29)); });
            Check("Reminder uses China wall time", () => { var m = new Memo { Date = "2026-09-28", Time = "20:00", Title = "test" }; var d = new Data(); d.Memos.Add(m); return Schedule.Due(d, new DateTime(2026, 9, 28, 11, 59, 0, DateTimeKind.Utc)).Count == 0 && Schedule.Due(d, new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc)).Count == 1; });
            Check("Missed repeated alerts coalesce and stay acknowledged", () => { var m = new Memo { Date = "2026-01-01", Time = "10:00", Title = "test", Repeat = "daily" }; var d = new Data(); d.Memos.Add(m); var utc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc); var due = Schedule.Due(d, utc); d.Acknowledged.Add(due[0].Key); return due.Count == 1 && due[0].When.Date == new DateTime(2026, 9, 28) && Schedule.Due(d, utc).Count == 0; });
            Check("Snooze survives midnight and persists", () => { var m = new Memo { Date = "2026-09-28", Time = "23:58", Title = "test", Repeat = "daily" }; var d = new Data(); d.Memos.Add(m); var o = Schedule.At(m, China.ParseDate(m.Date)); var utc = o.Utc.AddMinutes(1); d.Snoozes[o.Key] = utc.AddMinutes(10).Ticks; var copy = Repository.Deserialize(Repository.Serialize(d)); return Schedule.Due(copy, utc.AddMinutes(2)).Count == 0 && Schedule.Due(copy, utc.AddMinutes(11)).Count == 1; });
            Check("Completed and soft-deleted reminders are excluded", () => { var m = new Memo { Date = "2026-09-01", Title = "test" }; var d = new Data(); d.Memos.Add(m); d.Completed.Add(Schedule.At(m, China.ParseDate(m.Date)).Key); var utc = DateTime.UtcNow; if (Schedule.Due(d, utc).Count != 0) return false; d.Completed.Clear(); m.Deleted = true; return Schedule.Due(d, utc).Count == 0; });
            Check("DPAPI save, reload, atomic previous and daily backup", () => { var path = Path.Combine(folder, "storage-test-" + Guid.NewGuid().ToString("N")); var r = new Repository(path); r.Data.Memos.Add(new Memo { Title = "private-secret-测试" }); r.Save(); r.Data.Memos[0].Note = "changed"; r.Save(); var loaded = new Repository(path); return loaded.Data.Memos[0].Note == "changed" && !Encoding.UTF8.GetString(File.ReadAllBytes(r.PathName)).Contains("private-secret") && File.Exists(r.PathName + ".previous") && Directory.GetFiles(Path.Combine(path, "backups")).Length == 1; });
            Check("Portable encrypted backup round trip, wrong password and tamper", () => { var plain = Encoding.UTF8.GetBytes("private test 备忘"); var encrypted = PortableBackup.Encrypt(plain, "test-password-123"); if (!PortableBackup.Decrypt(encrypted, "test-password-123").SequenceEqual(plain)) return false; bool bad = false, tamper = false; try { PortableBackup.Decrypt(encrypted, "wrong-password"); } catch (System.Security.Cryptography.CryptographicException) { bad = true; } encrypted[50] ^= 1; try { PortableBackup.Decrypt(encrypted, "test-password-123"); } catch (System.Security.Cryptography.CryptographicException) { tamper = true; } return bad && tamper; });
            Check("Import merges without dropping unrelated existing memos", () => { var r = new Repository(Path.Combine(folder, "merge-test-" + Guid.NewGuid().ToString("N"))); r.Data.Memos.Add(new Memo { Title = "existing" }); r.Save(); var d = new Data(); d.Memos.Add(new Memo { Title = "incoming" }); return r.Merge(d) == 1 && r.Data.Memos.Count == 2; });
            Check("V1 settings migrate with transparent minimal defaults", () => {
                var legacy = Encoding.UTF8.GetBytes("{\"Version\":1,\"Memos\":[],\"Completed\":[],\"Acknowledged\":[],\"Snoozes\":{},\"Settings\":{\"Seconds\":true,\"Sound\":false,\"TogetherSince\":\"2024-01-01\"}}");
                var d = Repository.Deserialize(legacy); return d.Settings.Seconds && !d.Settings.Sound && d.Settings.TogetherSince == "2024-01-01" && d.Settings.ClockStyle == "transparent" && d.Settings.ClockScale == 1 && !d.Settings.LiveVisible && d.Settings.Theme == "system";
            });
            Check("Next reminder uses China midnight and skips handled items", () => {
                var d = new Data(); var a = new Memo { Title = "a", Date = "2026-09-29", Time = "00:10" }; var b = new Memo { Title = "b", Date = "2026-09-29", Time = "00:20" }; d.Memos.Add(a); d.Memos.Add(b);
                var utc = new DateTime(2026, 9, 28, 15, 59, 0, DateTimeKind.Utc); var first = LiveData.Next(d, utc); d.Completed.Add(first.Occurrence.Key); var next = LiveData.Next(d, utc);
                return first.Occurrence.Memo.Id == a.Id && (first.DueUtc - utc).TotalMinutes == 11 && next.Occurrence.Memo.Id == b.Id;
            });
            Check("Next reminder counts snooze across China midnight", () => {
                var d = new Data(); var m = new Memo { Title = "snooze", Date = "2026-09-28", Time = "23:58", Repeat = "daily" }; d.Memos.Add(m); var o = Schedule.At(m, China.ParseDate(m.Date)); var utc = o.Utc.AddMinutes(3); d.Snoozes[o.Key] = utc.AddMinutes(7).Ticks; var next = LiveData.Next(d, utc);
                return next.Snoozed && next.Occurrence.Key == o.Key && next.DueUtc == utc.AddMinutes(7);
            });
            Check("Leap anniversary next occurrence remains discoverable", () => {
                var d = new Data(); var m = new Memo { Title = "leap", Date = "2024-02-29", Time = "10:00", Repeat = "yearly" }; d.Memos.Add(m);
                var n = LiveData.Next(d, new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc)); return n.Occurrence.When == new DateTime(2028, 2, 29, 10, 0, 0);
            });
            Check("Disabled, deleted and completed reminders stay out of live countdown", () => {
                var d = new Data(); d.Memos.Add(new Memo { Title = "off", Date = "2027-01-01", Remind = false }); d.Memos.Add(new Memo { Title = "deleted", Date = "2027-01-01", Deleted = true });
                return LiveData.Next(d, new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)) == null;
            });
            Check("Monitor presets stay inside negative-coordinate and tiny displays", () => {
                var areas = new[] { new System.Drawing.Rectangle(-1920, -100, 1920, 1040), new System.Drawing.Rectangle(0, 0, 800, 560) };
                foreach (var a in areas) foreach (var mode in DisplayLayout.Placements) { var r = DisplayLayout.Place(a, 1300, 750, mode, 1, 0, 24); if (!a.Contains(r)) return false; } return true;
            });
            Check("Invalid display settings are rejected without accepting NaN", () => { var d = new Data(); d.Settings.ClockScale = double.NaN; try { Schedule.Validate(d); return false; } catch (InvalidDataException) { return true; } });
            Check("Anniversary day one and future countdown are China-date based", () => LiveData.Together("2026-09-28", new DateTime(2026, 9, 28)).Contains("1 天") && LiveData.Together("2026-09-29", new DateTime(2026, 9, 28)).Contains("1 天"));
            Check("Theme brushes stay editable through dark-light-dark transitions", () => { var s = new Settings { Theme = "dark" }; Theme.Apply(s, true); var dark = Ui.Ink.Color; bool mutable = !Ui.Ink.CanFreeze; s.Theme = "light"; Theme.Apply(s, true); var light = Ui.Ink.Color; s.Theme = "dark"; Theme.Apply(s, true); return mutable && !Ui.Ink.IsFrozen && dark != light && Ui.Ink.Color == dark; });
            var json = new JavaScriptSerializer().Serialize(Results); File.WriteAllText(Path.Combine(folder, "self-test.json"), json);
            return json.Contains("\"passed\":false") ? 1 : 0;
        }
    }
}
