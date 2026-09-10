using System;
using System.Runtime.InteropServices;

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

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint joystickId, ref JoyInfoEx info);

        private readonly uint _joystickId;
        private readonly string _name;

        public WinMmJoystickFallback(int joystickId, string name)
        {
            _joystickId = unchecked((uint)joystickId);
            _name = name;
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
                buttons[i] = (info.Buttons & (1u << i)) == 0 ? (byte)0 : CommonConstants.PRS128;
            return true;
        }
    }
}
