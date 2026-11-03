using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Mathematics;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using MdBlock = Markdig.Syntax.Block;

namespace LightMark
{
    internal sealed class OutlineEntry
    {
        internal int Offset;
        internal int Level;
        internal string Anchor;
        internal FrameworkContentElement Target;
        public string Title { get; set; }
        public override string ToString() { return Title; }
    }

    internal sealed class MarkdownView
    {
        internal const int PreviewLimit = 150000;
        internal static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseTaskLists().UseEmphasisExtras().UseAutoLinks().UseMathematics().Build();
        internal readonly List<OutlineEntry> Outline = new List<OutlineEntry>();
        internal Action<string> LinkClicked;
        private readonly string directory;
        private int blockCount;
        private int inlineCount;
        private int mathCount;
        private readonly AppTheme theme;
        private readonly Brush ink;
        private readonly Brush muted;
        private readonly Brush accent;

        internal MarkdownView(string path, bool dark = true) { directory = path == null ? null : Path.GetDirectoryName(path); theme = new AppTheme(dark); ink = BrushOf("#263348"); muted = BrushOf("#68768B"); accent = BrushOf("#28766D"); }

        internal FlowDocument Render(MarkdownDocument markdown, bool truncated)
        {
            Outline.Clear(); blockCount = 0; inlineCount = 0; mathCount = 0;
            var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"), FontSize = 16, Foreground = ink, Background = BrushOf("#FFFFFF"), PagePadding = new Thickness(36, 24, 36, 40), ColumnWidth = double.PositiveInfinity, LineHeight = 27 };
            if (truncated) document.Blocks.Add(new Paragraph(new Run("预览显示前 150,000 个字符，保存仍包含完整内容。")) { Foreground = muted, FontSize = 13 });
            foreach (MdBlock block in markdown) { if (blockCount >= 3000) break; document.Blocks.Add(ConvertBlock(block, 0)); }
            if (blockCount >= 3000) document.Blocks.Add(new Paragraph(new Run("[预览节点数量已达上限，保存仍保留完整源码]")) { Foreground = muted });
            return document;
        }

        private WpfBlock ConvertBlock(MdBlock block, int depth)
        {
            if (++blockCount > 3000 || depth > 32) return new Paragraph(new Run("[内容过多，已省略部分预览]"));
            var math = block as MathBlock;
            if (math != null)
            {
                return new BlockUIContainer(new ScrollViewer { Content = Formula(math.Lines.ToString(), true), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(8) }) { Margin = new Thickness(0, 10, 0, 16) };
            }
            var heading = block as HeadingBlock;
            if (heading != null)
            {
                var paragraph = ParagraphOf(heading.Inline);
                paragraph.FontSize = new double[] { 30, 25, 21, 18, 17, 16 }[Math.Min(5, heading.Level - 1)];
                paragraph.FontWeight = FontWeights.SemiBold;
                paragraph.LineHeight = paragraph.FontSize * 1.5;
                paragraph.Margin = new Thickness(0, heading.Level < 3 ? 22 : 14, 0, 12);
                if (heading.Level == 1) { paragraph.BorderBrush = BrushOf("#E5EAEF"); paragraph.BorderThickness = new Thickness(0, 0, 0, 1); paragraph.Padding = new Thickness(0, 0, 0, 12); }
                string title = InlineText(heading.Inline);
                string anchor = title.Trim().ToLowerInvariant().Replace(' ', '-');
                string baseAnchor = anchor; int duplicate = 0;
                while (Outline.Exists(delegate(OutlineEntry e) { return e.Anchor == anchor; })) anchor = baseAnchor + "-" + (++duplicate);
                Outline.Add(new OutlineEntry { Offset = block.Span.Start, Level = heading.Level, Title = title, Anchor = anchor, Target = paragraph });
                return paragraph;
            }
            var code = block as CodeBlock;
            if (code != null)
            {
                var paragraph = new Paragraph(new Run(code.Lines.ToString())) { FontFamily = new FontFamily("Consolas"), FontSize = 13, LineHeight = 21, Background = BrushOf("#F3F5F8"), Foreground = ink, Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 8, 0, 16), BorderBrush = BrushOf("#E6EBF0"), BorderThickness = new Thickness(1) };
                return paragraph;
            }
            var list = block as ListBlock;
            if (list != null)
            {
                int start; int.TryParse(list.OrderedStart, out start);
                var result = new System.Windows.Documents.List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, StartIndex = Math.Max(1, start), Padding = new Thickness(24, 0, 0, 0), Margin = new Thickness(0, 5, 0, 13) };
                foreach (MdBlock child in list)
                {
                    if (++blockCount >= 3000) break;
                    var item = new System.Windows.Documents.ListItem();
                    var container = child as ContainerBlock;
                    if (container != null) foreach (MdBlock nested in container) { if (blockCount >= 3000) break; item.Blocks.Add(ConvertBlock(nested, depth + 1)); }
                    result.ListItems.Add(item);
                }
                return result;
            }
            var quote = block as QuoteBlock;
            if (quote != null)
            {
                var section = new Section { BorderBrush = accent, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(16, 6, 10, 6), Margin = new Thickness(0, 8, 0, 16), Background = BrushOf("#F1F7F5"), Foreground = muted };
                foreach (MdBlock child in quote) { if (blockCount >= 3000) break; section.Blocks.Add(ConvertBlock(child, depth + 1)); }
                return section;
            }
            var table = block as Markdig.Extensions.Tables.Table;
            if (table != null)
            {
                var result = new System.Windows.Documents.Table { CellSpacing = 0, Margin = new Thickness(0, 10, 0, 18), FontSize = 14 };
                var group = new TableRowGroup(); result.RowGroups.Add(group);
                foreach (MdBlock child in table)
                {
                    if (++blockCount >= 3000) break;
                    var row = (Markdig.Extensions.Tables.TableRow)child;
                    var wpfRow = new System.Windows.Documents.TableRow { Background = row.IsHeader ? BrushOf("#F0F5F4") : BrushOf("#FFFFFF"), FontWeight = row.IsHeader ? FontWeights.SemiBold : FontWeights.Normal };
                    foreach (MdBlock cellBlock in row)
                    {
                        if (++blockCount >= 3000) break;
                        var cell = (Markdig.Extensions.Tables.TableCell)cellBlock;
                        var wpfCell = new System.Windows.Documents.TableCell { BorderBrush = BrushOf("#DFE5EB"), BorderThickness = new Thickness(0.5), Padding = new Thickness(10, 6, 10, 6), ColumnSpan = Math.Max(1, cell.ColumnSpan) };
                        foreach (MdBlock nested in cell) { if (blockCount >= 3000) break; wpfCell.Blocks.Add(ConvertBlock(nested, depth + 1)); }
                        wpfRow.Cells.Add(wpfCell);
                    }
                    group.Rows.Add(wpfRow);
                }
                return result;
            }
            if (block is ThematicBreakBlock) return new Paragraph { BorderBrush = BrushOf("#DCE4EB"), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 14, 0, 20), FontSize = 1 };
            var leaf = block as LeafBlock;
            if (leaf != null) return leaf.Inline != null ? ParagraphOf(leaf.Inline) : new Paragraph(new Run(leaf.Lines.ToString())) { Foreground = muted, Margin = new Thickness(0, 4, 0, 12) };
            var fallback = new Section();
            var children = block as ContainerBlock;
            if (children != null) foreach (MdBlock child in children) { if (blockCount >= 3000) break; fallback.Blocks.Add(ConvertBlock(child, depth + 1)); }
            return fallback;
        }

        private Paragraph ParagraphOf(ContainerInline source)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 3, 0, 12) };
            AddInlines(paragraph.Inlines, source, 0);
            return paragraph;
        }

        private void AddInlines(InlineCollection target, ContainerInline source, int depth)
        {
            if (source == null || depth > 32) return;
            foreach (Markdig.Syntax.Inlines.Inline inline in source)
            {
                if (++inlineCount > 30000) return;
                var math = inline as MathInline;
                if (math != null) { target.Add(new InlineUIContainer(Formula(math.Content.ToString(), math.DelimiterCount > 1)) { BaselineAlignment = BaselineAlignment.Center }); continue; }
                var literal = inline as LiteralInline;
                if (literal != null) { target.Add(new Run(literal.Content.ToString())); continue; }
                var code = inline as CodeInline;
                if (code != null) { target.Add(new Run(code.Content) { FontFamily = new FontFamily("Consolas"), Background = BrushOf("#EEF2F5"), Foreground = BrushOf("#A4475C"), FontSize = 14 }); continue; }
                if (inline is LineBreakInline) { if (((LineBreakInline)inline).IsHard) target.Add(new LineBreak()); else target.Add(new Run(" ")); continue; }
                var emphasis = inline as EmphasisInline;
                if (emphasis != null)
                {
                    var span = new Span();
                    if (emphasis.DelimiterChar == '~') span.TextDecorations = TextDecorations.Strikethrough;
                    else if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold;
                    else span.FontStyle = FontStyles.Italic;
                    AddInlines(span.Inlines, emphasis, depth + 1); target.Add(span); continue;
                }
                var link = inline as LinkInline;
                if (link != null)
                {
                    if (link.IsImage) { AddImage(target, link); continue; }
                    var hyperlink = new Hyperlink { Foreground = accent, ToolTip = link.Url };
                    AddInlines(hyperlink.Inlines, link, depth + 1);
                    string url = link.Url;
                    hyperlink.Click += delegate { if (LinkClicked != null) LinkClicked(url); };
                    target.Add(hyperlink); continue;
                }
                var auto = inline as AutolinkInline;
                if (auto != null) { var linkAuto = new Hyperlink(new Run(auto.Url)) { Foreground = accent }; string url = auto.Url; linkAuto.Click += delegate { if (LinkClicked != null) LinkClicked(url); }; target.Add(linkAuto); continue; }
                var task = inline as TaskList;
                if (task != null) { target.Add(new Run(task.Checked ? "☑ " : "☐ ") { Foreground = accent }); continue; }
                var html = inline as HtmlInline;
                if (html != null) { target.Add(new Run(html.Tag) { Foreground = muted }); continue; }
                var nested = inline as ContainerInline;
                if (nested != null) AddInlines(target, nested, depth + 1);
            }
        }

        private void AddImage(InlineCollection target, LinkInline link)
        {
            string label = InlineText(link);
            string path = LocalPath(directory, link.Url);
            string extension = path == null ? "" : Path.GetExtension(path).ToLowerInvariant();
            if (path != null && (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".gif" || extension == ".bmp"))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length <= 10 * 1024 * 1024)
                    {
                        var bitmap = new BitmapImage();
                        using (var stream = File.OpenRead(path)) { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 1400; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); }
                        target.Add(new InlineUIContainer(new Image { Source = bitmap, MaxWidth = 650, MaxHeight = 480, Stretch = Stretch.Uniform, ToolTip = label, Margin = new Thickness(0, 8, 0, 8) }));
                        return;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (NotSupportedException) { }
                catch (System.IO.FileFormatException) { }
                catch (ArgumentException) { }
            }
            target.Add(new Run("[图片：" + (label.Length == 0 ? link.Url : label) + "]") { Foreground = muted });
        }
        private FrameworkElement Formula(string source, bool display)
        {
            return ++mathCount > 128 ? MathView.Fallback(source, "单次预览最多排版 128 个公式", BrushOf("#8B0000")) : MathView.Create(source, display, ink, BrushOf("#8B0000"));
        }

        internal static string LocalPath(string directory, string url)
        {
            if (directory == null || string.IsNullOrEmpty(url)) return null;
            try
            {
                string decoded = Uri.UnescapeDataString(url);
                if (decoded.StartsWith("\\") || decoded.StartsWith("//") || decoded.Contains(":")) return null;
                string full = Path.GetFullPath(Path.Combine(directory, decoded.Replace('/', Path.DirectorySeparatorChar)));
                if (full.StartsWith("\\")) return null;
                return full;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
        }

        internal static string InlineText(ContainerInline inline)
        {
            var text = new StringBuilder();
            if (inline == null) return "";
            foreach (var child in inline)
            {
                var literal = child as LiteralInline; var code = child as CodeInline; var container = child as ContainerInline;
                if (literal != null) text.Append(literal.Content.ToString());
                else if (code != null) text.Append(code.Content);
                else if (container != null) text.Append(InlineText(container));
            }
            return text.ToString();
        }
        private Brush BrushOf(string color) { return theme.BrushFor(color); }
    }
}
