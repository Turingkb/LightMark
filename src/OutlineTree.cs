using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightMark
{
    internal static class OutlineTree
    {
        internal static void Populate(TreeView tree, IEnumerable<OutlineEntry> entries)
        {
            tree.Items.Clear();
            var parents = new Stack<TreeViewItem>();
            foreach (var entry in entries)
            {
                while (parents.Count > 0 && ((OutlineEntry)parents.Peek().Tag).Level >= entry.Level) parents.Pop();
                var header = new DockPanel { LastChildFill = true };
                var level = new TextBlock { Text = "H" + entry.Level, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.SlateGray, Width = 25, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(level, Dock.Left); header.Children.Add(level);
                header.Children.Add(new TextBlock { Text = entry.Title, TextWrapping = TextWrapping.Wrap, FontSize = entry.Level == 1 ? 14 : entry.Level == 2 ? 13 : 12, FontWeight = entry.Level <= 2 ? FontWeights.SemiBold : FontWeights.Normal, Foreground = entry.Level <= 2 ? new SolidColorBrush(Color.FromRgb(38, 51, 72)) : new SolidColorBrush(Color.FromRgb(92, 109, 130)) });
                var item = new TreeViewItem { Header = header, Tag = entry, ToolTip = entry.Title, IsExpanded = true, Margin = new Thickness(0, entry.Level <= 2 ? 4 : 1, 0, 0) };
                item.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "H" + entry.Level + " " + entry.Title);
                if (parents.Count == 0) tree.Items.Add(item); else parents.Peek().Items.Add(item);
                parents.Push(item);
            }
            if (tree.Items.Count == 0) tree.Items.Add(new TreeViewItem { Header = "标题会显示在这里", IsEnabled = false, FontSize = 11 });
        }
    }
}
