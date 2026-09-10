using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FalconBMS.Launcher.Input
{
    /// <summary>
    /// Button-only fallback for Wine's legacy DirectInput failure on CarrierAce MFDs.
    /// WinMM is intentionally used only after Managed DirectInput Acquire failed.
    /// </summary>
    internal sealed class WinMmJoystickFallback
    {
        private const int MaxButtons = 32;
        private const uint JoyReturnButtons = 0x00000080;
        private const uint JoyReturnAll = 0x000000FF;

        [StructLayout(LayoutKind.Sequential)]
        private struct JoyInfoEx
        {
            public uint Size;
            public uint Flags;
            public uint X;
            public uint Y;
            public uint Z;
            public uint R;
            public uint U;
            public uint V;
            public uint Buttons;
            public uint ButtonNumber;
            public uint Pov;
            public uint Reserved1;
            public uint Reserved2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct JoyCaps
        {
            public ushort ManufacturerId;
            public ushort ProductId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ProductName;
            public uint XMin;
            public uint XMax;
            public uint YMin;
            public uint YMax;
            public uint ZMin;
            public uint ZMax;
            public uint NumberButtons;
            public uint MinimumPeriod;
            public uint MaximumPeriod;
            public uint RMin;
            public uint RMax;
            public uint UMin;
            public uint UMax;
            public uint VMin;
            public uint VMax;
            public uint Caps;
            public uint MaxAxes;
            public uint NumberAxes;
            public uint MaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string RegistryKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string OemVxD;
        }

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint joystickId, ref JoyInfoEx info);

        [DllImport("winmm.dll")]
        private static extern uint joyGetNumDevs();

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern uint joyGetDevCaps(uint joystickId, ref JoyCaps caps, uint capsSize);

        private readonly uint _joystickId;
        private readonly string _name;

        public WinMmJoystickFallback(int joystickId, string name)
        {
            _joystickId = unchecked((uint)joystickId);
            _name = name;
        }

        /// <summary>
        /// Wine does not implement DirectInput's DeviceProperties.JoystickId. Match the
        /// DirectInput instance name against WinMM's device list instead.
        /// </summary>
        public static bool TryCreateForDevice(string deviceName, out WinMmJoystickFallback fallback, out string error)
        {
            fallback = null;
            uint deviceCount = joyGetNumDevs();
            string normalizedName = NormalizeName(deviceName);
            StringBuilder candidates = new StringBuilder();
            for (uint joystickId = 0; joystickId < deviceCount; ++joystickId)
            {
                JoyCaps caps = new JoyCaps();
                if (joyGetDevCaps(joystickId, ref caps, (uint)Marshal.SizeOf(typeof(JoyCaps))) != 0)
                    continue;

                if (candidates.Length != 0) candidates.Append(" | ");
                candidates.Append(joystickId).Append(":").Append(caps.ProductName);

                if (NormalizeName(caps.ProductName) == normalizedName)
                {
                    fallback = new WinMmJoystickFallback((int)joystickId, caps.ProductName);
                    error = null;
                    return true;
                }
            }

            error = "No WinMM device name matched '" + deviceName + "' (joyGetNumDevs=" + deviceCount + "; candidates=" + candidates + ").";
            return false;
        }

        private static string NormalizeName(string name)
        {
            if (String.IsNullOrEmpty(name)) return String.Empty;
            StringBuilder normalized = new StringBuilder(name.Length);
            foreach (char c in name)
                if (Char.IsLetterOrDigit(c)) normalized.Append(Char.ToUpperInvariant(c));
            return normalized.ToString();
        }

        public bool TryGetButtons(out byte[] buttons, out string error)
        {
            buttons = new byte[MaxButtons];
            error = null;
            JoyInfoEx info = new JoyInfoEx { Size = (uint)Marshal.SizeOf(typeof(JoyInfoEx)), Flags = JoyReturnAll | JoyReturnButtons };
            uint result = joyGetPosEx(_joystickId, ref info);
            if (result != 0)
            {
                error = "joyGetPosEx(" + _joystickId + ") returned " + result + " for " + _name;
                return false;
            }

            for (int i = 0; i < MaxButtons; ++i)
                buttons[i] = (byte)((info.Buttons & (1u << i)) == 0 ? CommonConstants.PRS0 : CommonConstants.PRS128);
            return true;
        }
    }
}
