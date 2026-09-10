using System;
using System.Runtime.InteropServices;

namespace FalconBMS.Launcher
{
    /// <summary>Small, dependency-free checks for workarounds which must not affect Windows.</summary>
    internal static class WineCompatibility
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procedureName);

        internal static bool IsRunningUnderWine()
        {
            try
            {
                // wine_get_version is exported by Wine's ntdll and is absent on Windows.
                IntPtr ntdll = GetModuleHandle("ntdll.dll");
                return ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }
    }
}
