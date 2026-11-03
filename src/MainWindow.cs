using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Markdig;

namespace LightMark
{
    internal sealed class MainWindow : Window
    {
        private readonly Grid root;
        private readonly TextBox editor;
        private readonly FlowDocumentScrollViewer preview;
        private readonly TreeView outline;
        private readonly LocalState settings = new LocalState();
        private readonly DispatcherTimer previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
        private readonly DispatcherTimer recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        private readonly string recoveryPath = Path.Combine(LocalState.RecoveryDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".md");
        private DocumentFile document = new DocumentFile();
        private string savedEditorText = "";
        private bool dirty;
        private bool loading;
        private bool closed;
        private bool checkingExternal;
        private bool externalAcknowledged;
        private bool parsing;
        private bool renderAgain;
        private int version;
        private int mode = 3;
        private MarkdownView rendered;
        private double scrollRatio;
        private AppTheme theme;
        private double? themeScrollRatio;

        internal MainWindow(string[] args)
        {
            using (var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("LightMark.MainWindow.xaml")) root = (Grid)XamlReader.Load(stream);
            Content = root;
            Title = "LightMark"; Width = settings.Width; Height = settings.Height; MinWidth = 860; MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.White;
            editor = Get<TextBox>("Editor"); preview = Get<FlowDocumentScrollViewer>("Preview"); outline = Get<TreeView>("OutlineTree");
            editor.FontSize = settings.FontSize;
            editor.TextChanged += delegate { if (loading) return; dirty = editor.Text != savedEditorText; version++; UpdateTitle(); UpdateStatistics(); QueuePreview(); recoveryTimer.Stop(); recoveryTimer.Start(); };
            editor.SelectionChanged += delegate { UpdateStatistics(); };
            editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(EditorScrolled));
            editor.PreviewKeyDown += EditorKeyDown;
            previewTimer.Tick += delegate { previewTimer.Stop(); RenderPreview(); };
            recoveryTimer.Tick += delegate { recoveryTimer.Stop(); SaveRecovery(); };
            outline.SelectedItemChanged += delegate { var item = outline.SelectedItem as TreeViewItem; if (item == null) return; var entry = item.Tag as OutlineEntry; if (entry == null) return; editor.CaretIndex = Math.Min(entry.Offset, editor.Text.Length); editor.ScrollToLine(editor.GetLineIndexFromCharacterIndex(editor.CaretIndex)); if (entry.Target != null) entry.Target.BringIntoView(); };
            WireCommands();
            PreviewKeyDown += WindowKeyDown;
            Closing += BeforeClose;
            Closed += delegate { closed = true; previewTimer.Stop(); recoveryTimer.Stop(); };
            Activated += delegate { CheckExternalChange(); };
            AllowDrop = true;
            PreviewDragOver += delegate(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; } };
            PreviewDrop += delegate(object sender, DragEventArgs e)
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop); e.Handled = true;
                if (paths.Length > 0) OpenPath(paths[0]);
            };
            Loaded += delegate
            {
                if (args.Length > 0) OpenPath(args[0]); else SetContent(WelcomeText(), new DocumentFile());
                SetMode(3); RefreshRecent();
                if (Directory.Exists(LocalState.RecoveryDirectory) && Directory.GetFiles(LocalState.RecoveryDirectory, "*.md").Length > 0) Status("发现未保存草稿，可从「文件 → 恢复未保存的草稿」找回。");
                else if (settings.Warning != null) Status(settings.Warning);
            };
            SetMode(3);
            ApplyTheme(settings.DarkTheme, false);
            SourceInitialized += delegate { theme.ApplyTitleBar(this); };
        }

        private T Get<T>(string name) where T : class { return (T)root.FindName(name); }
        private void Click(string name, Action action)
        {
            var button = root.FindName(name) as Button;
            if (button != null) button.Click += delegate { action(); };
            else Get<MenuItem>(name).Click += delegate { action(); };
        }
        private void WireCommands()
        {
            Click("NewMenu", NewDocument); Click("OpenMenu", OpenDialog); Click("OpenButton", OpenDialog);
            Click("SaveMenu", delegate { Save(false); }); Click("SaveButton", delegate { Save(false); }); Click("SaveAsMenu", delegate { Save(true); });
            Click("ExitMenu", Close); Click("RecoveryMenu", RecoverDraft); Click("FolderMenu", ShowFolder);
            Click("RegisterMenu", Register); Click("DefaultMenu", delegate { Launch("ms-settings:defaultapps"); });
            Click("UndoMenu", delegate { editor.Undo(); }); Click("RedoMenu", delegate { editor.Redo(); });
            Click("FindMenu", delegate { ShowFind(false); }); Click("ReplaceMenu", delegate { ShowFind(true); });
            Click("BoldMenu", delegate { Wrap("**", "**", "加粗文字"); }); Click("BoldButton", delegate { Wrap("**", "**", "加粗文字"); });
            Click("ItalicMenu", delegate { Wrap("*", "*", "斜体文字"); }); Click("LinkMenu", InsertLink); Click("LinkButton", InsertLink);
            Click("CodeButton", delegate { if (editor.SelectedText.Contains("\n")) Wrap("```\r\n", "\r\n```", "代码"); else Wrap("`", "`", "代码"); });
            Click("EditMenu", delegate { SetMode(1); }); Click("EditButton", delegate { SetMode(1); });
            Click("SplitMenu", delegate { SetMode(2); }); Click("SplitButton", delegate { SetMode(2); });
            Click("ReadMenu", delegate { SetMode(3); }); Click("ReadButton", delegate { SetMode(3); });
            Click("OutlineMenu", delegate { Get<ColumnDefinition>("OutlineColumn").Width = new GridLength(Get<MenuItem>("OutlineMenu").IsChecked ? 248 : 0); Get<FrameworkElement>("Sidebar").Visibility = Get<MenuItem>("OutlineMenu").IsChecked ? Visibility.Visible : Visibility.Collapsed; });
            Click("WrapMenu", delegate { editor.TextWrapping = Get<MenuItem>("WrapMenu").IsChecked ? TextWrapping.Wrap : TextWrapping.NoWrap; });
            Click("ZoomInMenu", delegate { Zoom(1); }); Click("ZoomOutMenu", delegate { Zoom(-1); });
            Click("FindNext", delegate { Find(false); }); Click("FindPrevious", delegate { Find(true); });
            Click("CloseFind", delegate { Get<Border>("FindBar").Visibility = Visibility.Collapsed; FocusDocument(); });
            Click("ReplaceOne", ReplaceOne); Click("ReplaceAll", ReplaceAll);
            Get<TextBox>("FindText").KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Find((Keyboard.Modifiers & ModifierKeys.Shift) != 0); e.Handled = true; } };
            Click("WelcomeMenu", delegate { if (ConfirmDiscard()) { SetContent(WelcomeText(), new DocumentFile()); SetMode(3); } });
            Click("LicensesMenu", ShowLicenses);
            Click("DarkThemeMenu", delegate { ApplyTheme(true, true); });
            Click("LightThemeMenu", delegate { ApplyTheme(false, true); });
            Click("ThemeButton", delegate { ApplyTheme(!settings.DarkTheme, true); });
            Click("AboutMenu", delegate { MessageBox.Show(this, "LightMark 1.0.4\n\n原生 Windows Markdown 编辑器\n本地编辑 · 离线预览 · 便携运行\n\n快捷键：Ctrl+1 编辑 / Ctrl+2 分栏 / Ctrl+3 阅读\n支持 CommonMark、表格、任务列表和本地图片。\nHTML 按源码显示，数学公式离线排版，Mermaid 按原文显示。\n\nMarkdown 解析：Markdig 1.5.0 (BSD-2-Clause)", "关于 LightMark", MessageBoxButton.OK, MessageBoxImage.Information); });
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (e.Key == Key.Escape) { Get<Border>("FindBar").Visibility = Visibility.Collapsed; FocusDocument(); e.Handled = true; return; }
            if (e.Key == Key.F3) { Find(shift); e.Handled = true; return; }
            if (!ctrl) return;
            switch (e.Key)
            {
                case Key.N: NewDocument(); break;
                case Key.O: OpenDialog(); break;
                case Key.S: Save(shift); break;
                case Key.F: ShowFind(false); break;
                case Key.H: ShowFind(true); break;
                case Key.B: Wrap("**", "**", "加粗文字"); break;
                case Key.I: Wrap("*", "*", "斜体文字"); break;
                case Key.K: InsertLink(); break;
                case Key.D1: case Key.NumPad1: SetMode(1); break;
                case Key.D2: case Key.NumPad2: SetMode(2); break;
                case Key.D3: case Key.NumPad3: SetMode(3); break;
                case Key.OemPlus: case Key.Add: Zoom(1); break;
                case Key.OemMinus: case Key.Subtract: Zoom(-1); break;
                default: return;
            }
            e.Handled = true;
        }

        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
            int caret = editor.CaretIndex;
            int start = caret == 0 ? 0 : Math.Max(editor.Text.LastIndexOf('\n', caret - 1), editor.Text.LastIndexOf('\r', caret - 1)) + 1;
            string prefix = editor.Text.Substring(start, editor.CaretIndex - start);
            var match = Regex.Match(prefix, @"^(\s*)([-+*] |\d+\. )(\[[ xX]\] )?(.*)$");
            if (match.Success)
            {
                e.Handled = true;
                if (match.Groups[4].Value.Length == 0) { editor.Select(start, prefix.Length); ReplaceSelection("\r\n"); return; }
                string marker = match.Groups[2].Value;
                int number;
                if (int.TryParse(marker.TrimEnd('.', ' '), out number) && number < int.MaxValue) marker = (number + 1) + ". ";
                ReplaceSelection("\r\n" + match.Groups[1].Value + marker + (match.Groups[3].Success ? "[ ] " : ""));
                editor.CaretIndex = editor.SelectionStart + editor.SelectionLength; editor.Select(editor.CaretIndex, 0);
            }
        }

        private void NewDocument() { if (ConfirmDiscard()) { SetContent("", new DocumentFile()); SetMode(2); } }
        private void OpenDialog()
        {
            var dialog = new OpenFileDialog { Filter = "Markdown 文件|*.md;*.markdown;*.mdown;*.mkd|文本文件|*.txt|所有文件|*.*", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) OpenPath(dialog.FileName);
        }
        private void OpenPath(string path)
        {
            if (!ConfirmDiscard()) return;
            try
            {
                var opened = DocumentFile.Open(path);
                SetContent(opened.Text, opened); SetMode(3); settings.Remember(opened.PathName); RefreshRecent(); Status("已打开 " + opened.PathName);
            }
            catch (Exception ex) { Error("无法打开文件", ex); }
        }
        private void SetContent(string text, DocumentFile file)
        {
            loading = true;
            try { document = file; editor.Text = text; savedEditorText = editor.Text; editor.IsUndoEnabled = false; editor.IsUndoEnabled = true; editor.CaretIndex = 0; dirty = false; externalAcknowledged = false; version++; }
            finally { loading = false; }
            DeleteRecovery(); UpdateTitle(); UpdateStatistics(); QueuePreview();
        }

        private bool Save(bool saveAs)
        {
            string path = document.PathName;
            if (saveAs || path == null)
            {
                var dialog = new SaveFileDialog { Filter = "Markdown 文件|*.md|所有文件|*.*", DefaultExt = ".md", AddExtension = true, FileName = path == null ? "未命名.md" : Path.GetFileName(path), OverwritePrompt = true };
                if (path != null) dialog.InitialDirectory = Path.GetDirectoryName(path);
                if (dialog.ShowDialog(this) != true) return false;
                path = dialog.FileName;
            }
            try
            {
                bool overwrite = false;
                if (string.Equals(path, document.PathName, StringComparison.OrdinalIgnoreCase) && document.ChangedOnDisk())
                {
                    if (MessageBox.Show(this, "此文件已被其他程序修改或删除：\n" + path + "\n\n是否用当前编辑内容覆盖？选择“否”后可另存为保留两个版本。", "检测到外部修改", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return false;
                    overwrite = true;
                }
                document.Save(path, editor.Text, overwrite); savedEditorText = editor.Text; dirty = false; externalAcknowledged = false;
                DeleteRecovery(); settings.Remember(path); RefreshRecent(); UpdateTitle(); UpdateStatistics(); QueuePreview(); Status("已保存 " + path); return true;
            }
            catch (Exception ex) { Error("保存失败，编辑内容仍保留", ex); return false; }
        }

        private bool ConfirmDiscard()
        {
            if (!dirty) return true;
            var choice = MessageBox.Show(this, "保存对“" + (document.PathName == null ? "未命名.md" : Path.GetFileName(document.PathName)) + "”的修改？", "未保存的修改", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) return false;
            return choice != MessageBoxResult.Yes || Save(false);
        }
        private void CheckExternalChange()
        {
            if (closed || checkingExternal || externalAcknowledged || document.PathName == null) return;
            checkingExternal = true;
            try
            {
                if (!document.ChangedOnDisk()) return;
                externalAcknowledged = true;
                if (!File.Exists(document.PathName)) { Status("磁盘文件已删除。当前内容保留，保存时会再次提示。"); return; }
                string detail = dirty ? "重新载入会替换当前未保存的编辑；草稿会先保留在恢复目录。" : "是否重新载入最新内容？";
                if (MessageBox.Show(this, "文件已在外部更新。\n\n" + detail, "文件已更改", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    if (dirty) SaveRecoveryCopy();
                    var opened = DocumentFile.Open(document.PathName); SetContent(opened.Text, opened);
                }
                else Status("已保留当前内容。保存时会再次检查磁盘版本。");
            }
            catch (Exception ex) { Status("无法检查磁盘文件：" + ex.Message); }
            finally { checkingExternal = false; }
        }

        private void QueuePreview() { previewTimer.Stop(); previewTimer.Start(); }
        private async void RenderPreview()
        {
            if (closed) return;
            if (parsing) { renderAgain = true; return; }
            parsing = true; int requestedVersion = version;
            string text = editor.Text; bool truncated = text.Length > MarkdownView.PreviewLimit;
            if (truncated) text = text.Substring(0, MarkdownView.PreviewLimit);
            try
            {
                var markdown = await Task.Run(delegate { return Markdown.Parse(text, MarkdownView.Pipeline); });
                if (closed || requestedVersion != version) { renderAgain = true; return; }
                var view = new MarkdownView(document.PathName, settings.DarkTheme); view.LinkClicked = OpenLink;
                preview.Document = view.Render(markdown, truncated); rendered = view;
                OutlineTree.Populate(outline, view.Outline);
                await Dispatcher.InvokeAsync(new Action(delegate {
                    var scroll = FindChild<ScrollViewer>(preview);
                    if (themeScrollRatio.HasValue && scroll != null) { scroll.ScrollToVerticalOffset(themeScrollRatio.Value * scroll.ScrollableHeight); themeScrollRatio = null; }
                    else SyncPreviewScroll();
                }), DispatcherPriority.Background);
            }
            catch (Exception ex) { Status("预览暂不可用，源码仍可编辑：" + ex.Message); }
            finally { parsing = false; if (renderAgain && !closed) { renderAgain = false; QueuePreview(); } }
        }

        private void OpenLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (url.StartsWith("#"))
            {
                string anchor = Uri.UnescapeDataString(url.Substring(1));
                var entry = rendered == null ? null : rendered.Outline.FirstOrDefault(item => item.Anchor == anchor);
                if (entry != null) entry.Target.BringIntoView(); else Status("未找到标题：" + anchor);
                return;
            }
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri) && (uri.Scheme == "http" || uri.Scheme == "https")) { Launch(uri.AbsoluteUri); return; }
            string pathPart = url.Split('#')[0];
            string path = MarkdownView.LocalPath(document.PathName == null ? null : Path.GetDirectoryName(document.PathName), pathPart);
            if (path != null && new string[] { ".md", ".markdown", ".mdown", ".mkd" }.Contains(Path.GetExtension(path).ToLowerInvariant())) { OpenPath(path); return; }
            Status("此链接未打开。支持网页、页内标题和本地 Markdown 文件。");
        }

        private void SetMode(int value)
        {
            mode = value;
            Get<ColumnDefinition>("EditorColumn").Width = new GridLength(value == 3 ? 0 : 1, value == 3 ? GridUnitType.Pixel : GridUnitType.Star);
            Get<ColumnDefinition>("PreviewColumn").Width = new GridLength(value == 1 ? 0 : 1, value == 1 ? GridUnitType.Pixel : GridUnitType.Star);
            Get<ColumnDefinition>("DividerColumn").Width = new GridLength(value == 2 ? 5 : 0);
            Get<FrameworkElement>("EditorPane").Visibility = value == 3 ? Visibility.Collapsed : Visibility.Visible;
            Get<FrameworkElement>("PreviewPane").Visibility = value == 1 ? Visibility.Collapsed : Visibility.Visible;
            Get<FrameworkElement>("Divider").Visibility = value == 2 ? Visibility.Visible : Visibility.Collapsed;
            foreach (string name in new string[] { "Edit", "Split", "Read" }) Get<Button>(name + "Button").Background = Brushes.Transparent;
            Get<Button>((value == 1 ? "Edit" : value == 2 ? "Split" : "Read") + "Button").SetResourceReference(Control.BackgroundProperty, AppTheme.Key("#FFFFFF"));
            Get<FrameworkElement>("FormattingTools").Visibility = value == 3 ? Visibility.Collapsed : Visibility.Visible;
            UpdateStatistics();
            FocusDocument();
        }
        private void FocusDocument() { if (mode == 3) preview.Focus(); else editor.Focus(); }
        private void ApplyTheme(bool dark, bool persist)
        {
            var scroll = FindChild<ScrollViewer>(preview);
            if (scroll != null) themeScrollRatio = scroll.ScrollableHeight > 0 ? scroll.VerticalOffset / scroll.ScrollableHeight : 0;
            settings.DarkTheme = dark; theme = new AppTheme(dark); theme.Apply(root);
            Background = theme.BrushFor("#FFFFFF"); theme.ApplyTitleBar(this);
            Get<MenuItem>("DarkThemeMenu").IsChecked = dark; Get<MenuItem>("LightThemeMenu").IsChecked = !dark;
            Get<Button>("ThemeButton").Content = dark ? "浅色模式" : "深色模式";
            version++; QueuePreview();
            if (persist)
            {
                try { settings.Save(); } catch (Exception ex) { Status("主题已切换，但偏好未保存：" + ex.Message); }
            }
        }

        private void EditorScrolled(object sender, ScrollChangedEventArgs e)
        {
            var scroll = e.OriginalSource as ScrollViewer;
            if (scroll == null || e.VerticalChange == 0) return;
            scrollRatio = scroll.ScrollableHeight <= 0 ? 0 : scroll.VerticalOffset / scroll.ScrollableHeight;
            SyncPreviewScroll();
        }
        private void SyncPreviewScroll()
        {
            if (closed || mode != 2 || !Get<MenuItem>("SyncMenu").IsChecked) return;
            var scroll = FindChild<ScrollViewer>(preview);
            if (scroll != null) scroll.ScrollToVerticalOffset(scrollRatio * scroll.ScrollableHeight);
        }
        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); var found = child as T; if (found != null) return found; found = FindChild<T>(child); if (found != null) return found; }
            return null;
        }
        private void Zoom(int delta) { editor.FontSize = Math.Max(11, Math.Min(28, editor.FontSize + delta)); preview.Zoom = Math.Max(70, Math.Min(160, preview.Zoom + delta * 5)); }
        private void Wrap(string before, string after, string placeholder)
        {
            if (mode == 3) SetMode(2);
            int start = editor.SelectionStart; string text = editor.SelectedText;
            if (text.Length == 0) text = placeholder;
            ReplaceSelection(before + text + after); editor.Select(start + before.Length, text.Length); editor.Focus();
        }
        private void ReplaceSelection(string text)
        {
            editor.BeginChange();
            try { editor.SelectedText = text; }
            finally { editor.EndChange(); }
        }
        private void InsertLink() { Wrap("[", "](https://example.com)", "链接文字"); }

        private void ShowFind(bool replace)
        {
            Get<Border>("FindBar").Visibility = Visibility.Visible;
            Get<StackPanel>("ReplaceBar").Visibility = replace ? Visibility.Visible : Visibility.Collapsed;
            if (editor.SelectionLength > 0 && !editor.SelectedText.Contains("\n")) Get<TextBox>("FindText").Text = editor.SelectedText;
            Get<TextBox>("FindText").Focus(); Get<TextBox>("FindText").SelectAll();
        }
        private StringComparison Comparison { get { return Get<CheckBox>("MatchCase").IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase; } }
        private void Find(bool backwards)
        {
            string query = Get<TextBox>("FindText").Text; if (query.Length == 0) { ShowFind(false); return; }
            string text = editor.Text; int index = -1;
            if (text.Length > 0)
            {
                if (backwards) { int start = editor.SelectionStart - 1; if (start >= 0) index = text.LastIndexOf(query, start, Comparison); if (index < 0) index = text.LastIndexOf(query, Comparison); }
                else { int start = Math.Min(text.Length, editor.SelectionStart + editor.SelectionLength); index = text.IndexOf(query, start, Comparison); if (index < 0) index = text.IndexOf(query, Comparison); }
            }
            if (index < 0) { Get<TextBlock>("FindInfo").Text = "未找到"; return; }
            if (mode == 3) SetMode(2);
            editor.Select(index, query.Length); editor.ScrollToLine(editor.GetLineIndexFromCharacterIndex(index)); editor.Focus(); Get<TextBlock>("FindInfo").Text = "已找到 · F3 继续";
        }
        private void ReplaceOne()
        {
            string query = Get<TextBox>("FindText").Text;
            if (query.Length == 0) return;
            if (string.Equals(editor.SelectedText, query, Comparison)) { int start = editor.SelectionStart; ReplaceSelection(Get<TextBox>("ReplaceText").Text); editor.CaretIndex = start + Get<TextBox>("ReplaceText").Text.Length; editor.Select(editor.CaretIndex, 0); }
            Find(false);
        }
        private void ReplaceAll()
        {
            string query = Get<TextBox>("FindText").Text; if (query.Length == 0) return;
            string text = editor.Text; string replacement = Get<TextBox>("ReplaceText").Text; int count = 0, offset = 0, index;
            var result = new StringBuilder();
            while ((index = text.IndexOf(query, offset, Comparison)) >= 0) { result.Append(text, offset, index - offset); result.Append(replacement); offset = index + query.Length; count++; }
            result.Append(text, offset, text.Length - offset);
            if (count > 0) { editor.SelectAll(); ReplaceSelection(result.ToString()); }
            Get<TextBlock>("FindInfo").Text = "已替换 " + count + " 处";
        }

        private void UpdateTitle()
        {
            string name = document.PathName == null ? "未命名.md" : Path.GetFileName(document.PathName);
            Title = (dirty ? "● " : "") + name + " — LightMark";
            Get<TextBlock>("FileLabel").Text = name; Get<TextBlock>("FileLabel").ToolTip = document.PathName;
            Get<TextBlock>("DirtyLabel").Text = dirty ? "● 未保存" : document.PathName == null ? "新文档" : "已保存";
        }
        private void UpdateStatistics()
        {
            string newline = document.NewLine == "\r\n" ? "CRLF" : document.NewLine == "\n" ? "LF" : "CR";
            if (mode == 3)
            {
                Get<TextBlock>("Statistics").Text = string.Format("{0:N0} 字符    {1}    {2}", editor.Text.Length, document.EncodingName, newline);
                return;
            }
            int line = Math.Max(0, editor.GetLineIndexFromCharacterIndex(editor.CaretIndex));
            int lineStart = Math.Max(0, editor.GetCharacterIndexFromLineIndex(line));
            Get<TextBlock>("Statistics").Text = string.Format("{0:N0} 字符    行 {1}，列 {2}    {3}    {4}", editor.Text.Length, line + 1, editor.CaretIndex - lineStart + 1, document.EncodingName, newline);
        }
        private void RefreshRecent()
        {
            var menu = Get<MenuItem>("RecentMenu"); menu.Items.Clear();
            foreach (string path in settings.Recent) { var item = new MenuItem { Header = path.Replace("_", "__"), ToolTip = path }; string selected = path; item.Click += delegate { OpenPath(selected); }; menu.Items.Add(item); }
            menu.IsEnabled = menu.Items.Count > 0;
        }
        private void SaveRecovery()
        {
            if (!dirty) { DeleteRecovery(); return; }
            try { Directory.CreateDirectory(LocalState.RecoveryDirectory); AtomicDraft(recoveryPath, editor.Text); Status("未保存草稿已备份 · Ctrl+S 保存到文件"); }
            catch (Exception ex) { Status("草稿备份失败，请手动保存：" + ex.Message); }
        }
        private void SaveRecoveryCopy()
        {
            Directory.CreateDirectory(LocalState.RecoveryDirectory);
            AtomicDraft(Path.Combine(LocalState.RecoveryDirectory, "外部更新前-" + Guid.NewGuid().ToString("N") + ".md"), editor.Text);
        }
        private static void AtomicDraft(string path, string text)
        {
            string temporary = path + ".tmp";
            try { File.WriteAllText(temporary, text, new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private void DeleteRecovery()
        {
            try { if (File.Exists(recoveryPath)) File.Delete(recoveryPath); }
            catch (Exception ex) { Status("旧草稿未清理：" + ex.Message); }
        }
        private void RecoverDraft()
        {
            Directory.CreateDirectory(LocalState.RecoveryDirectory);
            var dialog = new OpenFileDialog { Title = "选择要恢复的草稿（恢复后请另存为）", InitialDirectory = LocalState.RecoveryDirectory, Filter = "恢复草稿|*.md", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true || !ConfirmDiscard()) return;
            try { string text = DocumentFile.Open(dialog.FileName).Text; SetContent(text, new DocumentFile()); SetMode(2); dirty = true; savedEditorText = ""; UpdateTitle(); SaveRecovery(); Status("草稿已恢复，请按 Ctrl+S 另存为。原草稿仍保留。"); }
            catch (Exception ex) { Error("草稿恢复失败", ex); }
        }
        private void ShowFolder()
        {
            if (document.PathName == null) { Status("请先将文档保存到文件。"); return; }
            try { Process.Start("explorer.exe", "/select,\"" + document.PathName + "\""); } catch (Exception ex) { Error("无法打开文件夹", ex); }
        }
        private void Register()
        {
            try { FileAssociation.Register(); MessageBox.Show(this, "已将 LightMark 添加到 Markdown 的“打开方式”。\n\n在接下来的系统设置中搜索 .md，选择 LightMark。\n也可右键 .md 文件 → 打开方式 → 选择其他应用 → LightMark → 始终。", "设置默认打开方式", MessageBoxButton.OK, MessageBoxImage.Information); Launch("ms-settings:defaultapps"); }
            catch (Exception ex) { Error("注册失败", ex); }
        }
        private void ShowLicenses()
        {
            string text;
            using (var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("LightMark.ThirdParty.txt")) using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
            var licenseWindow = new Window { Owner = this, Title = "第三方许可证", Width = 760, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20), BorderThickness = new Thickness(0), FontSize = 13 } };
            theme.Apply(licenseWindow);
            ((TextBox)licenseWindow.Content).Background = theme.BrushFor("#FFFFFF");
            ((TextBox)licenseWindow.Content).Foreground = theme.BrushFor("#263348");
            licenseWindow.SourceInitialized += delegate { theme.ApplyTitleBar(licenseWindow); };
            licenseWindow.ShowDialog();
        }
        private void Launch(string target) { try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch (Exception ex) { Error("无法打开", ex); } }
        private void BeforeClose(object sender, CancelEventArgs e)
        {
            if (!ConfirmDiscard()) { e.Cancel = true; return; }
            DeleteRecovery();
            try { settings.Width = RestoreBounds.Width; settings.Height = RestoreBounds.Height; settings.FontSize = editor.FontSize; settings.Save(); }
            catch (Exception ex) { MessageBox.Show(this, "文档操作已完成，但窗口设置未保存：\n" + ex.Message, "LightMark"); }
        }
        private void Status(string text) { Get<TextBlock>("Message").Text = text; Get<TextBlock>("Message").ToolTip = text; }
        private void Error(string title, Exception ex) { MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error); Status(title); }
        private static string WelcomeText()
        {
            using (var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("LightMark.Welcome.md")) using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
        }
    }
}
