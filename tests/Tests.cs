using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Markdig;

namespace LightMark
{
    internal static class Tests
    {
        private static int passed;
        private static string directory;
        [STAThread]
        private static int Main(string[] args)
        {
            directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("LIGHTMARK_DATA_DIR", Path.Combine(directory, "state"));
            try
            {
                RoundTrip(new UTF8Encoding(false, true), "UTF8", "\n");
                RoundTrip(new UTF8Encoding(true, true), "UTF8-BOM", "\r\n");
                RoundTrip(new UnicodeEncoding(false, true, true), "UTF16-LE", "\r\n");
                RoundTrip(new UnicodeEncoding(true, true, true), "UTF16-BE", "\n");
                RoundTrip(new UTF32Encoding(false, true, true), "UTF32-LE", "\n");
                RoundTrip(new UTF32Encoding(true, true, true), "UTF32-BE", "\r\n");
                RoundTrip(Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), "GB18030", "\r\n");
                ConflictSafety(); NewFile(); FailedSave(); RejectInvalid(); Render(); OutlineHierarchy(); XamlLayout(); WindowCommands();
                Console.WriteLine("PASS: " + passed + " assertions");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { Directory.Delete(directory, true); }
        }

        private static void Check(bool value, string message) { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
        private static void RoundTrip(Encoding encoding, string name, string newline)
        {
            string path = Path.Combine(directory, name + " 中文 空格.md");
            string content = "# 中文标题" + newline + "第一行 café 😀" + newline;
            File.WriteAllText(path, content, encoding); byte[] original = File.ReadAllBytes(path);
            var file = DocumentFile.Open(path);
            Check(file.Text == content, name + " decodes Chinese and emoji");
            file.Save(path, content.Replace("\r\n", "\n").Replace("\n", "\r\n"), false);
            Check(original.SequenceEqual(File.ReadAllBytes(path)), name + " preserves bytes, BOM and newline through WPF normalization");
            Check(!file.ChangedOnDisk(), name + " saved fingerprint matches disk");
        }
        private static void ConflictSafety()
        {
            string path = Path.Combine(directory, "conflict.md"); File.WriteAllText(path, "original");
            var file = DocumentFile.Open(path); File.WriteAllText(path, "external change");
            Check(file.ChangedOnDisk(), "detects external file change");
            bool rejected = false; try { file.Save(path, "editor change", false); } catch (IOException) { rejected = true; }
            Check(rejected && File.ReadAllText(path) == "external change", "conflicting save never overwrites disk");
            file.Save(path, "explicit overwrite", true);
            Check(File.ReadAllText(path) == "explicit overwrite", "explicit overwrite succeeds");
            File.Delete(path);
            Check(file.ChangedOnDisk(), "detects external deletion");
            rejected = false; try { file.Save(path, "editor change", false); } catch (IOException) { rejected = true; }
            Check(rejected && !File.Exists(path), "deleted file requires explicit overwrite");
            file.Save(path, "restored", true);
            Check(File.ReadAllText(path) == "restored", "explicitly restores deleted file");
        }
        private static void NewFile()
        {
            var file = new DocumentFile(); string path = Path.Combine(directory, "new.md"); file.Save(path, "中文\n第二行", false);
            byte[] bytes = File.ReadAllBytes(path);
            Check(bytes[0] != 0xef && File.ReadAllText(path) == "中文\r\n第二行", "new files use UTF8 without BOM and CRLF");
            Check(Directory.GetFiles(directory, ".lightmark-*.tmp").Length == 0, "atomic save cleans temporary files");
            file.Save(Path.Combine(directory, "另存为.md"), "new path", false);
            Check(File.ReadAllText(path) == "中文\r\n第二行", "save as leaves source unchanged");
        }
        private static void FailedSave()
        {
            string path = Path.Combine(directory, "failure.md"); File.WriteAllText(path, "safe"); var file = DocumentFile.Open(path);
            bool rejected = false;
            try { file.Save(Path.Combine(directory, "missing", "failure.md"), "lost?", false); } catch (DirectoryNotFoundException) { rejected = true; }
            Check(rejected && File.ReadAllText(path) == "safe" && file.PathName == path, "save failure preserves original and document identity");
        }
        private static void RejectInvalid()
        {
            string path = Path.Combine(directory, "binary.md"); File.WriteAllBytes(path, new byte[] { 0, 1, 2 });
            bool rejected = false; try { DocumentFile.Open(path); } catch (IOException) { rejected = true; }
            Check(rejected, "binary input rejected");
            File.WriteAllBytes(path, new byte[] { 0xff, 0xfe, 0x00 });
            rejected = false; try { DocumentFile.Open(path); } catch (IOException) { rejected = true; }
            Check(rejected, "invalid BOM encoding rejected without replacement characters");
            using (var stream = File.Create(path)) stream.SetLength(DocumentFile.MaxBytes + 1);
            rejected = false; try { DocumentFile.Open(path); } catch (IOException) { rejected = true; }
            Check(rejected, "oversized file rejected before decoding");
        }
        private static void Render()
        {
            string markdown = "# 标题\n\n## 子标题\n\n## 子标题\n\n**加粗** *斜体* ~~删除~~ `代码` [网页](https://example.com)\n\n> 引用\n\n- [x] 完成\n- [ ] 待办\n\n```cs\nvar x = 1;\n```\n\n| A | B |\n|---|---|\n| 中 | 文 |\n\n<script>alert(1)</script>\n\n![远程](https://example.com/tracker.png)\n";
            var view = new MarkdownView(Path.Combine(directory, "test.md"));
            var document = view.Render(Markdown.Parse(markdown, MarkdownView.Pipeline), false);
            string plain = new TextRange(document.ContentStart, document.ContentEnd).Text;
            Check(view.Outline.Count == 3 && view.Outline[2].Anchor == "子标题-1", "heading outline and duplicate anchors");
            Check(plain.Contains("☑") && plain.Contains("☐"), "task list checked and unchecked states rendered");
            Check(document.Blocks.OfType<Table>().Count() == 1, "pipe table converted to native table");
            Check(plain.Contains("var x = 1;"), "fenced code content preserved");
            Check(plain.Contains("<script>alert(1)</script>"), "HTML remains inert visible source");
            Check(plain.Contains("[图片：远程]"), "remote image is a placeholder");
            Check(MarkdownView.LocalPath(directory, "https://example.com/x.png") == null && MarkdownView.LocalPath(directory, "//server/image.png") == null && MarkdownView.LocalPath(directory, "%5C%5Cserver%5Cimage.png") == null && MarkdownView.LocalPath(directory, "file:///C:/x.png") == null, "network and file URI image paths rejected");
            File.WriteAllBytes(Path.Combine(directory, "pixel.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aC1kAAAAASUVORK5CYII="));
            var withImage = new MarkdownView(Path.Combine(directory, "test.md")).Render(Markdown.Parse("![本地](pixel.png)", MarkdownView.Pipeline), false);
            Check(withImage.Blocks.OfType<Paragraph>().First().Inlines.OfType<InlineUIContainer>().Count() == 1, "relative local PNG decoded into native image");
            string many = string.Concat(Enumerable.Repeat("paragraph\n\n", 3500));
            var bounded = new MarkdownView(null).Render(Markdown.Parse(many, MarkdownView.Pipeline), false);
            Check(bounded.Blocks.Count <= 3001 && new TextRange(bounded.ContentStart, bounded.ContentEnd).Text.Contains("节点数量已达上限"), "many short blocks obey actual preview node budget");
        }
        private static void XamlLayout()
        {
            var window = new MainWindow(new string[0]);
            var root = (Grid)window.Content;
            Check(((Grid)root.FindName("EditorPane")).Visibility == Visibility.Collapsed, "source is hidden before first window layout");
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Check(root.FindName("Editor") is TextBox && root.FindName("Preview") is FlowDocumentScrollViewer, "real UI XAML loads and exposes editor and preview");
            var editor = (TextBox)root.FindName("Editor");
            var preview = (FlowDocumentScrollViewer)root.FindName("Preview");
            Pump(600);
            root.Measure(new Size(1240, 780)); root.Arrange(new Rect(0, 0, 1240, 780)); root.UpdateLayout();
            Check(((Grid)root.FindName("EditorPane")).Visibility == Visibility.Collapsed && preview.ActualWidth > 900, "welcome opens with full reading pane");
            Check(((StackPanel)root.FindName("FormattingTools")).Visibility == Visibility.Collapsed, "reading mode hides formatting tools");
            Check(!((TextBlock)root.FindName("Statistics")).Text.Contains("行 "), "reading statistics do not report a hidden editor caret");
            var image = new RenderTargetBitmap(1240, 780, 96, 96, PixelFormats.Pbgra32); image.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
            using (var output = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-preview.png"))) png.Save(output);
            ((Button)root.FindName("SplitButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); root.UpdateLayout();
            Check(editor.ActualWidth > 350 && preview.ActualWidth > 350, "split panes retain usable width at 1240px");
        }
        private static void OutlineHierarchy()
        {
            var entries = new OutlineEntry[] {
                new OutlineEntry { Level = 1, Title = "文档" }, new OutlineEntry { Level = 2, Title = "章节一" },
                new OutlineEntry { Level = 3, Title = "小节" }, new OutlineEntry { Level = 6, Title = "跳级标题" },
                new OutlineEntry { Level = 2, Title = "章节二" }, new OutlineEntry { Level = 1, Title = "另一文档" }
            };
            var tree = new TreeView(); OutlineTree.Populate(tree, entries);
            var first = (TreeViewItem)tree.Items[0]; var section = (TreeViewItem)first.Items[0];
            var subsection = (TreeViewItem)section.Items[0];
            Check(tree.Items.Count == 2 && first.Items.Count == 2 && section.Items.Count == 1 && subsection.Items.Count == 1, "outline nests headings under the nearest lower-level parent");
            Check(((OutlineEntry)((TreeViewItem)first.Items[1]).Tag).Title == "章节二", "outline returns to sibling level after deep headings");
            var header = (DockPanel)first.Header; var childHeader = (DockPanel)subsection.Header;
            Check(((TextBlock)header.Children[0]).Text == "H1" && ((TextBlock)((DockPanel)((TreeViewItem)subsection.Items[0]).Header).Children[0]).Text == "H6", "outline shows explicit H1 through H6 labels");
            Check(((TextBlock)header.Children[1]).FontSize > ((TextBlock)childHeader.Children[1]).FontSize && ((TextBlock)header.Children[1]).FontWeight == FontWeights.SemiBold, "main and child headings differ in size and weight");
            first.IsExpanded = false; Check(!first.IsExpanded && first.Items.Count == 2, "outline can collapse without losing child headings");
            first.IsExpanded = true; Check(first.IsExpanded, "outline can expand again");
            OutlineTree.Populate(tree, new OutlineEntry[] { new OutlineEntry { Level = 3, Title = "无一级标题" } });
            Check(tree.Items.Count == 1 && ((OutlineEntry)((TreeViewItem)tree.Items[0]).Tag).Level == 3, "outline accepts a document starting at H3");
        }
        private static void Pump(int milliseconds)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
        }
        private static void WindowCommands()
        {
            string path = Path.Combine(directory, "命令行带空格 中文.md");
            File.WriteAllText(path, "# 实际文件\n\nfirst first\n", new UTF8Encoding(false));
            var window = new MainWindow(new string[] { path });
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var root = (Grid)window.Content;
            root.Measure(new Size(1240, 780)); root.Arrange(new Rect(0, 0, 1240, 780)); root.UpdateLayout();
            var editor = (TextBox)root.FindName("Editor");
            Check(editor.Text.Contains("实际文件") && window.Title.Contains("命令行带空格 中文.md"), "command-line file with Chinese and spaces opens in actual window");
            Check(((Grid)root.FindName("EditorPane")).Visibility == Visibility.Collapsed && ((Grid)root.FindName("PreviewPane")).Visibility == Visibility.Visible, "command-line Markdown opens in preview mode");
            var read = (Button)root.FindName("ReadButton"); read.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((Grid)root.FindName("EditorPane")).Visibility == Visibility.Collapsed, "reading command hides editor");
            ((Button)root.FindName("SplitButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            root.UpdateLayout();
            Check(((Grid)root.FindName("EditorPane")).Visibility == Visibility.Visible && ((Grid)root.FindName("PreviewPane")).Visibility == Visibility.Visible, "split command restores both panes");
            int start = editor.Text.IndexOf("first", StringComparison.Ordinal); editor.Select(start, 5);
            ((Button)root.FindName("BoldButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(editor.Text.Contains("**first**") && window.Title.StartsWith("●"), "format command edits source and marks document dirty");
            Check(editor.CanUndo, "format command creates an undo action");
            editor.Undo(); Check(!editor.Text.Contains("**"), "native undo restores unformatted text");
            ((MenuItem)root.FindName("ReplaceMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            ((TextBox)root.FindName("FindText")).Text = "first"; ((TextBox)root.FindName("ReplaceText")).Text = "$1 中文";
            ((Button)root.FindName("ReplaceAll")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(editor.Text.Contains("$1 中文 $1 中文"), "replace all treats replacement as literal text");
            editor.Undo(); Check(editor.Text.Contains("first first"), "replace all is one native undo action");
            ((TextBox)root.FindName("FindText")).Text = "FIRST";
            ((Button)root.FindName("FindNext")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(editor.SelectedText == "first", "find is case-insensitive by default");
            ((CheckBox)root.FindName("MatchCase")).IsChecked = true;
            ((Button)root.FindName("FindNext")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((TextBlock)root.FindName("FindInfo")).Text == "未找到", "case-sensitive find rejects different case");
            editor.CaretIndex = editor.Text.Length; editor.SelectedText = "\r\n草稿保护";
            Pump(3400);
            Check(Directory.Exists(LocalState.RecoveryDirectory) && Directory.GetFiles(LocalState.RecoveryDirectory, "*.md").Length == 1, "unsaved actual window creates one recovery draft after debounce");
            string draft = Directory.GetFiles(LocalState.RecoveryDirectory, "*.md")[0];
            Check(File.ReadAllText(draft) == editor.Text, "recovery draft contains full unsaved source");
            var preview = (FlowDocumentScrollViewer)root.FindName("Preview");
            Check(preview.Document != null && new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text.Contains("草稿保护"), "async preview updates to current source in actual window");
            ((Button)root.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(DocumentFile.Open(path).Text.Contains("草稿保护") && !window.Title.StartsWith("●"), "save button writes actual file and clears dirty state");
            Check(DocumentFile.Open(path).NewLine == "\n", "actual window save preserves original LF newline");
            Check(Directory.GetFiles(LocalState.RecoveryDirectory, "*.md").Length == 0, "successful save clears current recovery draft");
            Pump(600);
            var tree = (TreeView)root.FindName("OutlineTree");
            var heading = (TreeViewItem)tree.Items[0]; heading.IsSelected = true;
            Check(editor.CaretIndex == 0, "selecting a tree heading navigates to its source offset");
            var recent = (MenuItem)root.FindName("RecentMenu");
            ((MenuItem)recent.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(((Grid)root.FindName("EditorPane")).Visibility == Visibility.Collapsed, "opening recent file resets editing mode to reading");
            ((MenuItem)root.FindName("NewMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(editor.Text.Length == 0 && ((Grid)root.FindName("EditorPane")).Visibility == Visibility.Visible, "explicit new document opens an editor");
        }
    }
}
