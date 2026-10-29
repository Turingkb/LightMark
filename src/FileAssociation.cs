using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LightMark
{
    internal static class FileAssociation
    {
        private const string ProgId = "LightMark.Markdown";
        [DllImport("shell32.dll")] private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
        internal static void Register()
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            string command = "\"" + exe + "\" \"%1\"";
            Set(@"Software\Classes\" + ProgId, "", "Markdown 文档");
            Set(@"Software\Classes\" + ProgId + @"\DefaultIcon", "", "\"" + exe + "\",0");
            Set(@"Software\Classes\" + ProgId + @"\shell\open\command", "", command);
            Set(@"Software\Classes\Applications\LightMark.exe", "FriendlyAppName", "LightMark");
            Set(@"Software\Classes\Applications\LightMark.exe\shell\open\command", "", command);
            foreach (string extension in new string[] { ".md", ".markdown", ".mdown", ".mkd" })
            {
                Set(@"Software\Classes\" + extension + @"\OpenWithProgids", ProgId, "");
                Set(@"Software\Classes\Applications\LightMark.exe\SupportedTypes", extension, "");
                Set(@"Software\LightMark\Capabilities\FileAssociations", extension, ProgId);
            }
            Set(@"Software\LightMark\Capabilities", "ApplicationName", "LightMark");
            Set(@"Software\LightMark\Capabilities", "ApplicationDescription", "轻量 Markdown 编辑器与离线预览");
            Set(@"Software\RegisteredApplications", "LightMark", @"Software\LightMark\Capabilities");
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
        private static void Set(string path, string name, string value) { using (var key = Registry.CurrentUser.CreateSubKey(path)) key.SetValue(name, value, RegistryValueKind.String); }
    }
}
