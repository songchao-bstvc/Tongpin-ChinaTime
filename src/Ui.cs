using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.Generic;

namespace Tongpin
{
    public static class Ui
    {
        sealed class Tone : INotifyPropertyChanged
        {
            Color color; public Color Color { get { return color; } set { color = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Color")); } }
            public event PropertyChangedEventHandler PropertyChanged;
        }
        static readonly Dictionary<SolidColorBrush, Tone> Tones = new Dictionary<SolidColorBrush, Tone>();
        public static SolidColorBrush AdaptiveBrush(string hex)
        {
            var tone = new Tone { Color = (Color)ColorConverter.ConvertFromString(hex) }; var brush = new SolidColorBrush();
            BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding("Color") { Source = tone }); Tones[brush] = tone; return brush;
        }
        public static void SetColor(SolidColorBrush brush, Color color) { Tone tone; if (Tones.TryGetValue(brush, out tone)) tone.Color = color; else brush.Color = color; }
        public static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        public static readonly SolidColorBrush Ink = AdaptiveBrush("#F1F3F7"), Muted = AdaptiveBrush("#ADB6C4"), Accent = AdaptiveBrush("#A9C8EF"), Green = AdaptiveBrush("#ABD6C7"), Background = AdaptiveBrush("#20242B"), Surface = AdaptiveBrush("#D22A3039"), Control = AdaptiveBrush("#393F49"), Input = AdaptiveBrush("#CE191E26"), Line = AdaptiveBrush("#557D899B"), Selected = AdaptiveBrush("#425975"), Glass = AdaptiveBrush("#8820242B"), Faint = AdaptiveBrush("#727E90"), AccentInk = AdaptiveBrush("#172A40");
        public static void Style(Window w)
        {
            w.FontFamily = new FontFamily("Microsoft YaHei UI"); w.FontSize = 13; w.Foreground = Ink; w.Background = Glass;
            w.UseLayoutRounding = true; w.SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(w, TextRenderingMode.Grayscale);
            var resources = Application.Current.Resources;
            resources["Ink"] = Ink; resources["Muted"] = Muted; resources["Accent"] = Accent;
            resources["Control"] = Control; resources["Input"] = Input; resources["Line"] = Line;
            resources["Surface"] = Surface; resources["Background"] = Background; resources["Selected"] = Selected;
            w.Resources = (ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='Button'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Control}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='12,8'/><Setter Property='Margin' Value='0,0,8,0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='bg' Background='{TemplateBinding Background}' BorderBrush='Transparent' BorderThickness='1' Padding='{TemplateBinding Padding}' CornerRadius='8'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bg' Property='Opacity' Value='0.8'/><Setter TargetName='bg' Property='BorderBrush' Value='{DynamicResource Line}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='bg' Property='BorderBrush' Value='{DynamicResource Line}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='bg' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='TextBox'><Setter Property='Background' Value='{DynamicResource Input}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='CaretBrush' Value='{DynamicResource Accent}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='Padding' Value='10,7'/><Setter Property='Margin' Value='0,4,0,12'/></Style>
<Style TargetType='CheckBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Margin' Value='0,6,0,6'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
<Style TargetType='ComboBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Control}'/><Setter Property='Margin' Value='0,4,0,12'/><Setter Property='Padding' Value='10,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border CornerRadius='7' Background='{DynamicResource Control}' BorderBrush='{DynamicResource Line}' BorderThickness='1'><Grid><TextBlock Text='⌄' Foreground='{DynamicResource Muted}' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,0'/></Grid></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Margin='12,9,34,9' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'/><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'><Border Background='{DynamicResource Background}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='8' Padding='5' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'><ScrollViewer MaxHeight='320'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='10,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='bg' Background='Transparent' Padding='{TemplateBinding Padding}' CornerRadius='5'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='bg' Property='Background' Value='{DynamicResource Selected}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='bg' Property='Background' Value='{DynamicResource Selected}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='PasswordBox'><Setter Property='Background' Value='{DynamicResource Input}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='Padding' Value='10,8'/><Setter Property='Margin' Value='0,6,0,12'/></Style>
<Style TargetType='ToolTip'><Setter Property='Background' Value='{DynamicResource Background}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='Padding' Value='12'/></Style>
<Style TargetType='ContextMenu'><Setter Property='Background' Value='{DynamicResource Background}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='9' Padding='5'><ScrollViewer CanContentScroll='True' HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/></ScrollViewer></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='TabControl'><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/></Style>
<Style TargetType='TabItem'><Setter Property='Foreground' Value='{DynamicResource Muted}'/><Setter Property='Margin' Value='0,0,8,0'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabItem'><Border x:Name='bg' CornerRadius='8' Background='Transparent' Padding='14,10' Margin='0,0,0,16'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter TargetName='bg' Property='Background' Value='{DynamicResource Control}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='Slider'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'><Grid Height='24' Background='Transparent'><Track x:Name='PART_Track' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='3' Background='{DynamicResource Accent}' CornerRadius='2'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='14' Height='14'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='{DynamicResource Accent}' BorderBrush='{DynamicResource Background}' BorderThickness='2' CornerRadius='7'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='3' Background='{DynamicResource Control}' CornerRadius='2'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='MenuItem'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='12,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'><Grid><Border x:Name='bg' Background='Transparent' CornerRadius='6' Padding='{TemplateBinding Padding}'><Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='20'/></Grid.ColumnDefinitions><ContentPresenter ContentSource='Header' RecognizesAccessKey='True'/><TextBlock x:Name='arrow' Grid.Column='1' Text='›' HorizontalAlignment='Right' Visibility='Collapsed'/></Grid></Border><Popup x:Name='PART_Popup' AllowsTransparency='True' Placement='Right' IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}' Focusable='False'><Border Background='{DynamicResource Background}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='8' Padding='4'><ItemsPresenter/></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='HasItems' Value='True'><Setter TargetName='arrow' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='bg' Property='Background' Value='{DynamicResource Control}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>");
            w.SourceInitialized += (s, e) => Native.Decorate(w);
        }
        public static TextBlock Text(string text, double size = 13, Brush color = null)
        { return new TextBlock { Text = text, FontSize = size, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap }; }
        public static Button Button(string text, Action action, bool accent = false)
        {
            var b = new Button { Content = text, Background = accent ? Accent : Control, Foreground = accent ? AccentInk : Ink };
            b.Click += (s, e) => { try { action(); } catch (Exception ex) { Error(ex); } }; return b;
        }
        public static Border Card(UIElement content, Thickness padding, Brush color = null)
        { return new Border { Child = content, Background = color ?? Surface, CornerRadius = new CornerRadius(12), Padding = padding }; }
        public static void Error(Exception ex) { App.Log("error", ex.GetType().Name); MessageBox.Show(ex.Message, "同频 · 操作未完成", MessageBoxButton.OK, MessageBoxImage.Warning); }
        public static StackPanel Row(params UIElement[] elements) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var e in elements) p.Children.Add(e); return p; }
        public static void Label(StackPanel p, string text) { p.Children.Add(new TextBlock { Text = text, Foreground = Muted, Margin = new Thickness(0, 8, 0, 0) }); }
        public static Window Dialog(string title, double width, double height, Window owner = null)
        {
            var w = new Window { Title = title, Width = width, Height = height, WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = owner };
            Style(w); return w;
        }
        public static string Password(Window owner, bool confirm)
        {
            var w = Dialog(confirm ? "为备份设置密码" : "输入备份密码", 410, confirm ? 310 : 230, owner);
            var p = new StackPanel { Margin = new Thickness(24) }; w.Content = p;
            p.Children.Add(Text(confirm ? "用这个密码，可以在另一台电脑恢复备忘。\n请妥善保管，密码遗失后无法恢复。" : "请输入导出这个备份时设置的密码。", 13, Muted));
            var one = new PasswordBox(); p.Children.Add(one); PasswordBox two = null;
            if (confirm) { p.Children.Add(Text("再次输入密码", 12, Muted)); two = new PasswordBox(); p.Children.Add(two); }
            string result = null;
            p.Children.Add(Button("继续", () => {
                if (one.Password.Length < 8 || (confirm && one.Password != two.Password)) { MessageBox.Show("密码至少 8 个字符，两次输入需一致。", "备份密码"); return; }
                result = one.Password; w.DialogResult = true;
            }, true));
            w.Loaded += (s, e) => one.Focus(); w.ShowDialog(); return result;
        }
    }
}
