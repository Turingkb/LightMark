using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

[assembly: AssemblyTitle("LightMark")]
[assembly: AssemblyDescription("轻量 Markdown 编辑器")]
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]

namespace LightMark
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;
            try { Run(args); }
            catch (Exception ex) { MessageBox.Show("LightMark 启动失败：\n" + ex.Message, "LightMark", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private static Assembly ResolveEmbedded(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dependencies." + name + ".dll"))
            {
                if (stream == null) return null;
                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length) { int n = stream.Read(bytes, read, bytes.Length - read); if (n == 0) throw new EndOfStreamException(); read += n; }
                return Assembly.Load(bytes);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run(string[] args)
        {
            TaskbarIdentity.Initialize();
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                MessageBox.Show("操作未完成，当前文字仍保留在编辑区。\n\n" + e.Exception.Message, "LightMark", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            app.Run(new MainWindow(args));
        }
    }
}
