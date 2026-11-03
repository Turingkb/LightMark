using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMath;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;
using XamlMath.Exceptions;

namespace LightMark
{
    internal static class MathView
    {
        internal static FrameworkElement Create(string source, bool display)
        {
            try
            {
                if (source.Length > 4096) return Fallback(source, "公式超过 4096 字符");
                int depth = 0;
                foreach (char c in source) { if (c == '{' && ++depth > 48) return Fallback(source, "公式嵌套过深"); if (c == '}') depth--; }
                double size = display ? 21 : 16;
                var formula = WpfTeXFormulaParser.Instance.Parse(source);
                var environment = WpfTeXEnvironment.Create(display ? TexStyle.Display : TexStyle.Text, size, "Segoe UI");
                var geometry = formula.RenderToGeometry(environment, size);
                Rect bounds = geometry.Bounds;
                if (bounds.IsEmpty || !IsFinite(bounds.Width) || !IsFinite(bounds.Height) || bounds.Width > 8000 || bounds.Height > 2000) return Fallback(source, "公式尺寸超出预览范围");
                geometry.Freeze();
                var drawing = new GeometryDrawing(Brushes.Black, null, geometry); drawing.Freeze();
                var image = new Image { Source = new DrawingImage(drawing), Width = Math.Max(1, bounds.Width), Height = Math.Max(1, bounds.Height), Stretch = Stretch.Uniform, ToolTip = source, Margin = new Thickness(2, 3, 2, 3) };
                System.Windows.Automation.AutomationProperties.SetName(image, "公式 " + source);
                return image;
            }
            catch (TexException ex) { return Fallback(source, ex.Message); }
            catch (ArgumentException ex) { return Fallback(source, ex.Message); }
            catch (InvalidOperationException ex) { return Fallback(source, ex.Message); }
        }
        private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        internal static FrameworkElement Fallback(string source, string reason)
        {
            return new TextBlock { Text = source, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkRed, ToolTip = "公式未排版，保留原文：" + reason };
        }
    }
}
