using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LightMark
{
    internal sealed class AppTheme
    {
        internal readonly bool Dark;
        // The existing light palette is the stable key shared by XAML and document rendering.
        private static readonly Dictionary<string, string> DarkColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            {"#FFFFFF", "#191F28"}, {"#F7F9FB", "#151A22"}, {"#F6F8FA", "#171D26"}, {"#FAFBFD", "#161C25"},
            {"#263348", "#E2E8F0"}, {"#3F4F63", "#CAD4E1"}, {"#68768B", "#A4B2C5"}, {"#68788D", "#A4B2C5"},
            {"#75849A", "#97A9BE"}, {"#7E8A9B", "#97A9BE"}, {"#8794A5", "#91A3B9"}, {"#8190A1", "#91A3B9"},
            {"#78899A", "#97A9BE"}, {"#66768B", "#A4B2C5"}, {"#28766D", "#76CCB9"}, {"#216C62", "#93DFCE"},
            {"#E6EFED", "#2A3A3D"}, {"#DFEEEA", "#29483F"}, {"#E5F1ED", "#254239"},
            {"#E2E8EF", "#323D4B"}, {"#DDE4EC", "#364353"}, {"#E4E9EF", "#323D4B"}, {"#E5EBF1", "#303B49"},
            {"#E1E7EE", "#303B49"}, {"#E6EBF1", "#303B49"}, {"#DDE5ED", "#303B49"}, {"#DEE7EA", "#303B49"},
            {"#F0F3F6", "#11161D"}, {"#EFF4F5", "#1F2834"}, {"#F3F6F8", "#141A22"},
            {"#E5EAEF", "#354252"}, {"#F3F5F8", "#222C3A"}, {"#E6EBF0", "#354252"},
            {"#F1F7F5", "#20332F"}, {"#F0F5F4", "#263D37"}, {"#DFE5EB", "#3E4A5A"},
            {"#DCE4EB", "#354252"}, {"#EEF2F5", "#293344"}, {"#A4475C", "#F3A5B8"}, {"#8B0000", "#FFA6A6"}
        };
        internal AppTheme(bool dark) { Dark = dark; }
        internal Brush BrushFor(string lightColor)
        {
            string color;
            if (!Dark || !DarkColors.TryGetValue(lightColor, out color)) color = lightColor;
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color); brush.Freeze(); return brush;
        }
        internal void Apply(FrameworkElement root)
        {
            foreach (var pair in DarkColors) root.Resources[Key(pair.Key)] = BrushFor(pair.Key);
            root.Resources[SystemColors.ControlBrushKey] = BrushFor("#F6F8FA");
            root.Resources[SystemColors.WindowBrushKey] = BrushFor("#FFFFFF");
            root.Resources[SystemColors.WindowTextBrushKey] = BrushFor("#263348");
            root.Resources[SystemColors.ControlTextBrushKey] = BrushFor("#263348");
            root.Resources[SystemColors.HighlightBrushKey] = BrushFor("#DFEEEA");
            root.Resources[SystemColors.HighlightTextBrushKey] = BrushFor("#263348");
        }
        internal static string Key(string color) { return "Theme." + color.TrimStart('#').ToUpperInvariant(); }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        internal void ApplyTitleBar(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            int dark = Dark ? 1 : 0;
            // Older Windows builds can reject this attribute; retain the system frame there.
            DwmSetWindowAttribute(handle, 20, ref dark, 4);
        }
    }
}
