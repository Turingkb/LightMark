using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LightMark
{
    internal sealed class DocumentFile
    {
        internal const int MaxBytes = 8 * 1024 * 1024;
        internal string PathName { get; private set; }
        internal string Text { get; private set; }
        internal Encoding Encoding { get; private set; }
        internal string NewLine { get; private set; }
        private byte[] fingerprint;
        internal string EncodingName { get { return Encoding.CodePage == 65001 ? (Encoding.GetPreamble().Length > 0 ? "UTF-8 BOM" : "UTF-8") : Encoding.EncodingName; } }

        internal DocumentFile() { Text = ""; Encoding = new UTF8Encoding(false, true); NewLine = "\r\n"; }

        internal static DocumentFile Open(string path)
        {
            string full = Path.GetFullPath(path);
            if (new FileInfo(full).Length > MaxBytes) throw new IOException("文件超过 8 MB。请使用适合大型文本的编辑器。");
            byte[] bytes = File.ReadAllBytes(full);
            if (bytes.Length > MaxBytes) throw new IOException("文件超过 8 MB，无法打开。");
            var file = new DocumentFile();
            int skip = 0;
            if (Starts(bytes, new byte[] { 0xff, 0xfe, 0, 0 })) { file.Encoding = new UTF32Encoding(false, true, true); skip = 4; }
            else if (Starts(bytes, new byte[] { 0, 0, 0xfe, 0xff })) { file.Encoding = new UTF32Encoding(true, true, true); skip = 4; }
            else if (Starts(bytes, new byte[] { 0xef, 0xbb, 0xbf })) { file.Encoding = new UTF8Encoding(true, true); skip = 3; }
            else if (Starts(bytes, new byte[] { 0xff, 0xfe })) { file.Encoding = new UnicodeEncoding(false, true, true); skip = 2; }
            else if (Starts(bytes, new byte[] { 0xfe, 0xff })) { file.Encoding = new UnicodeEncoding(true, true, true); skip = 2; }
            try { file.Text = file.Encoding.GetString(bytes, skip, bytes.Length - skip); }
            catch (DecoderFallbackException)
            {
                if (skip > 0) throw new IOException("文件编码无效，未读取或修改文件。");
                // Chinese Windows documents commonly use GB18030. Strict decoding prevents replacement-character data loss.
                file.Encoding = Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                file.Text = file.Encoding.GetString(bytes);
            }
            if (file.Text.IndexOf('\0') >= 0) throw new IOException("文件包含二进制内容，无法作为 Markdown 打开。");
            int lf = file.Text.IndexOf('\n');
            file.NewLine = lf >= 0 ? (lf > 0 && file.Text[lf - 1] == '\r' ? "\r\n" : "\n") : (file.Text.Contains("\r") ? "\r" : "\r\n");
            file.PathName = full;
            file.fingerprint = Hash(bytes);
            return file;
        }

        internal bool ChangedOnDisk()
        {
            if (PathName == null) return false;
            if (!File.Exists(PathName)) return true;
            using (var input = new FileStream(PathName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sha = SHA256.Create()) return !Same(fingerprint, sha.ComputeHash(input));
        }

        internal void Save(string path, string text, bool allowOverwrite)
        {
            string full = Path.GetFullPath(path);
            bool samePath = string.Equals(full, PathName, StringComparison.OrdinalIgnoreCase);
            if (samePath && !allowOverwrite && ChangedOnDisk()) throw new IOException("磁盘文件已被其他程序修改。请先重新载入，或明确选择覆盖。");
            // WPF uses CRLF internally. Restore the original file's newline convention on save.
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", NewLine);
            byte[] body = Encoding.GetBytes(normalized);
            byte[] preamble = Encoding.GetPreamble();
            byte[] bytes = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
            string temporary = Path.Combine(Path.GetDirectoryName(full), ".lightmark-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                // Recheck after encoding/writing, so changes made while preparing the save are caught.
                if (samePath && !allowOverwrite && ChangedOnDisk()) throw new IOException("保存期间文件被其他程序修改，请重试。");
                if (File.Exists(full)) File.Replace(temporary, full, null);
                else File.Move(temporary, full);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            PathName = full; Text = normalized; fingerprint = Hash(bytes);
        }

        private static bool Starts(byte[] bytes, byte[] prefix)
        {
            if (bytes.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++) if (bytes[i] != prefix[i]) return false;
            return true;
        }
        private static byte[] Hash(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
        private static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
