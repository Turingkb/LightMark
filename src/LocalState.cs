using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace LightMark
{
    internal sealed class LocalState
    {
        internal readonly List<string> Recent = new List<string>();
        internal static string DirectoryName
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("LIGHTMARK_DATA_DIR");
                return string.IsNullOrWhiteSpace(configured) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightMark") : Path.GetFullPath(configured);
            }
        }
        internal static string RecoveryDirectory { get { return Path.Combine(DirectoryName, "Recovery"); } }
        internal double Width = 1240;
        internal double Height = 820;
        internal double FontSize = 15;
        internal string Warning;

        internal LocalState()
        {
            string path = Path.Combine(DirectoryName, "settings.xml");
            if (!File.Exists(path)) return;
            try
            {
                var root = XDocument.Load(path).Root;
                double value;
                if (double.TryParse((string)root.Attribute("width"), out value)) Width = Math.Max(860, Math.Min(3000, value));
                if (double.TryParse((string)root.Attribute("height"), out value)) Height = Math.Max(540, Math.Min(2000, value));
                if (double.TryParse((string)root.Attribute("fontSize"), out value)) FontSize = Math.Max(11, Math.Min(28, value));
                Recent.AddRange(root.Elements("recent").Select(e => e.Value).Where(File.Exists).Take(10));
            }
            catch (Exception ex) { Warning = "设置读取失败，已使用默认布局：" + ex.Message; }
        }

        internal void Remember(string path)
        {
            Recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)); Recent.Insert(0, path);
            if (Recent.Count > 10) Recent.RemoveRange(10, Recent.Count - 10);
        }

        internal void Save()
        {
            Directory.CreateDirectory(DirectoryName);
            var root = new XElement("settings", new XAttribute("width", Width), new XAttribute("height", Height), new XAttribute("fontSize", FontSize), Recent.Select(p => new XElement("recent", p)));
            string path = Path.Combine(DirectoryName, "settings.xml");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { new XDocument(root).Save(temporary); if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
