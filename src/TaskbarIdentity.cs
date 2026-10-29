using System.Runtime.InteropServices;

namespace LightMark
{
    internal static class TaskbarIdentity
    {
        // Keep the identity independent of file names, executable paths and release versions.
        internal const string AppId = "Turingkb.LightMark";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        internal static void Initialize()
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppId));
        }
    }
}
