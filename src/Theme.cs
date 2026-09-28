using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Tongpin
{
    public static class Theme
    {
        public static bool Dark = true, TaskbarDark = true, GlassEnabled;
        public static string Fingerprint = "";
        public static int Read(string path, string name, int fallback)
        { try { using (var k = Registry.CurrentUser.OpenSubKey(path)) { var v = k?.GetValue(name); return v is int ? (int)v : fallback; } } catch { return fallback; } }
        const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        public static bool Apply(Settings s, bool force = false)
        {
            bool hc = SystemParameters.HighContrast;
            bool appDark = Read(Personalize, "AppsUseLightTheme", 0) == 0;
            bool barDark = Read(Personalize, "SystemUsesLightTheme", 0) == 0;
            bool transparency = Read(Personalize, "EnableTransparency", 1) != 0;
            string key = s.Theme + s.Material + appDark + barDark + transparency + hc;
            if (!force && key == Fingerprint) return false;
            Fingerprint = key;
            Dark = s.Theme == "dark" || (s.Theme == "system" && appDark);
            // The taskbar can use a different theme from application windows.
            TaskbarDark = barDark;
            GlassEnabled = s.Material == "glass" && transparency && !hc;
            Set(Ui.Ink, Dark ? "#F1F3F7" : "#202630");
            Set(Ui.Muted, Dark ? "#ADB6C4" : "#596575");
            Set(Ui.Faint, Dark ? "#727E90" : "#7D8998");
            Set(Ui.Accent, Dark ? "#A9C8EF" : "#285A8D");
            Set(Ui.AccentInk, Dark ? "#172A40" : "#FFFFFF");
            Set(Ui.Green, Dark ? "#ABD6C7" : "#32705D");
            Set(Ui.Background, Dark ? "#20242B" : "#F5F7FA");
            Set(Ui.Surface, Dark ? "#D22A3039" : "#DAFFFFFF");
            Set(Ui.Control, Dark ? "#393F49" : "#E6EBF1");
            Set(Ui.Input, Dark ? "#CE191E26" : "#E8FFFFFF");
            Set(Ui.Line, Dark ? "#557D899B" : "#4465768C");
            Set(Ui.Selected, Dark ? "#425975" : "#D2E3F6");
            Set(Ui.Glass, GlassEnabled ? (Dark ? "#8820242B" : "#99F6F8FC") : (Dark ? "#FF20242B" : "#FFF5F7FA"));
            if (hc)
            {
                foreach (var b in new[] { Ui.Ink, Ui.Muted, Ui.Faint }) Ui.SetColor(b, SystemColors.WindowTextColor);
                foreach (var b in new[] { Ui.Background, Ui.Surface, Ui.Input, Ui.Glass }) Ui.SetColor(b, SystemColors.WindowColor);
                Ui.SetColor(Ui.Accent, SystemColors.HighlightColor); Ui.SetColor(Ui.Selected, SystemColors.HighlightColor); Ui.SetColor(Ui.AccentInk, SystemColors.HighlightTextColor);
                Ui.SetColor(Ui.Control, SystemColors.ControlColor); Ui.SetColor(Ui.Line, SystemColors.WindowTextColor);
            }
            return true;
        }
        static void Set(SolidColorBrush b, string hex) { Ui.SetColor(b, (Color)ColorConverter.ConvertFromString(hex)); }
        public static void FadeIn(Window window, Settings s)
        {
            if (!s.Motion || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) return;
            window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.88, 1, TimeSpan.FromMilliseconds(140)) { FillBehavior = FillBehavior.Stop });
        }
    }
}
