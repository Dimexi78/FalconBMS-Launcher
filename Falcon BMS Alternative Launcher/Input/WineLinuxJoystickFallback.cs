using System;
using System.IO;
using System.Text;
using System.Threading;

namespace FalconBMS.Launcher.Input
{
    /// <summary>
    /// Wine-only button reader for devices which Wine's legacy Managed DirectInput cannot
    /// acquire. Linux's joystick API preserves all of the CarrierAce MFD's 50 buttons.
    /// </summary>
    internal sealed class WineLinuxJoystickFallback
    {
        private const int MaxButtons = CommonConstants.DX_MAX_BUTTONS;
        private const byte JsEventButton = 0x01;
        private const byte JsEventTypeMask = 0x7f;

        private readonly FileStream stream;
        private readonly byte[] buttons = new byte[MaxButtons];
        private readonly object syncRoot = new object();
        private Exception readException;

        public string DevicePath { get; private set; }

        private WineLinuxJoystickFallback(FileStream stream, string devicePath)
        {
            this.stream = stream;
            DevicePath = devicePath;

            Thread reader = new Thread(ReadEvents);
            reader.IsBackground = true;
            reader.Name = "Wine Linux joystick fallback";
            reader.Start();
        }

        public static bool TryCreateForDevice(string deviceName, out WineLinuxJoystickFallback fallback, out string error)
        {
            fallback = null;
            string normalizedName = NormalizeName(deviceName);
            StringBuilder candidates = new StringBuilder();

            // Linux joystick indices are stable for the lifetime of the running launcher.
            for (int index = 0; index < 32; ++index)
            {
                string sysNamePath = @"Z:\sys\class\input\js" + index + @"\device\name";
                if (!File.Exists(sysNamePath)) continue;

                string linuxDeviceName;
                try
                {
                    linuxDeviceName = File.ReadAllText(sysNamePath).Trim();
                }
                catch (Exception ex)
                {
                    error = "Unable to read " + sysNamePath + ": " + ex.Message;
                    return false;
                }

                if (candidates.Length != 0) candidates.Append(" | ");
                candidates.Append("js").Append(index).Append(":").Append(linuxDeviceName);
                if (NormalizeName(linuxDeviceName) != normalizedName) continue;

                string devicePath = @"Z:\dev\input\js" + index;
                try
                {
                    fallback = new WineLinuxJoystickFallback(
                        new FileStream(devicePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), devicePath);
                    error = null;
                    return true;
                }
                catch (Exception ex)
                {
                    error = "Unable to open " + devicePath + ": " + ex.Message;
                    return false;
                }
            }

            error = "No Linux joystick name matched '" + deviceName + "' (candidates=" + candidates + ").";
            return false;
        }

        public bool TryGetButtons(out byte[] currentButtons, out string error)
        {
            lock (syncRoot)
            {
                currentButtons = new byte[buttons.Length];
                Array.Copy(buttons, currentButtons, buttons.Length);
                error = readException == null ? null : readException.Message;
                return readException == null;
            }
        }

        private void ReadEvents()
        {
            try
            {
                byte[] jsEvent = new byte[8]; // struct js_event: uint time, short value, byte type, byte number
                while (ReadFully(jsEvent))
                {
                    byte eventType = (byte)(jsEvent[6] & JsEventTypeMask);
                    int buttonNumber = jsEvent[7];
                    short value = BitConverter.ToInt16(jsEvent, 4);
                    if (eventType != JsEventButton || buttonNumber >= buttons.Length) continue;

                    lock (syncRoot)
                        buttons[buttonNumber] = value == 0 ? (byte)0 : (byte)CommonConstants.PRS128;
                }
            }
            catch (Exception ex)
            {
                lock (syncRoot)
                    readException = ex;
                Diagnostics.Log("Wine Linux joystick fallback failed for " + DevicePath + ": " + ex.Message,
                    Diagnostics.LogLevels.Warning);
            }
        }

        private bool ReadFully(byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int bytesRead = stream.Read(buffer, offset, buffer.Length - offset);
                if (bytesRead == 0) return false;
                offset += bytesRead;
            }
            return true;
        }

        private static string NormalizeName(string name)
        {
            if (String.IsNullOrEmpty(name)) return String.Empty;
            StringBuilder normalized = new StringBuilder(name.Length);
            foreach (char c in name)
                if (Char.IsLetterOrDigit(c)) normalized.Append(Char.ToUpperInvariant(c));
            return normalized.ToString();
        }
    }
}
