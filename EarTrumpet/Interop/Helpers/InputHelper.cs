using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EarTrumpet.Interop.Helpers
{
    class InputHelper
    {
        public static void RegisterForMouseInput(IntPtr handle)
        {
            var data = new User32.RAWINPUTDEVICE
            {
                usUsagePage = User32.HidUsagePage.GENERIC,
                usUsage = User32.HidUsage.Mouse,
                dwFlags = User32.RIDEV_INPUTSINK,
                hwndTarget = handle
            };

            if (!RegisterRawInputDevices(data))
            {
                Trace.WriteLine($"InputHelper UnregisterForMouseInput: {Marshal.GetLastWin32Error()}");
            }
        }

        public static void UnregisterForMouseInput()
        {
            var data = new User32.RAWINPUTDEVICE
            {
                usUsagePage = User32.HidUsagePage.GENERIC,
                usUsage = User32.HidUsage.Mouse,
                dwFlags = User32.RIDEV_REMOVE
            };

            if (!RegisterRawInputDevices(data))
            {
                Trace.WriteLine($"InputHelper UnregisterForMouseInput: {Marshal.GetLastWin32Error()}");
            }
        }

        private static bool RegisterRawInputDevices(User32.RAWINPUTDEVICE data)
        {
            var devicePtr = Marshal.AllocHGlobal(Marshal.SizeOf(data));
            Marshal.StructureToPtr(data, devicePtr, false);
            bool ret = User32.RegisterRawInputDevices(devicePtr, 1, (uint)Marshal.SizeOf(data));
            Marshal.FreeHGlobal(devicePtr);
            return ret;
        }

        // Cheap per-message parse: a gaming mouse sends thousands of WM_INPUT per second,
        // so only wheel messages are decoded and nothing is allocated.
        private static readonly object _rawLock = new object();
        private static IntPtr _rawBuffer = Marshal.AllocHGlobal(256);
        private const uint RawBufferSize = 256;

        public static bool TryGetWheelDelta(IntPtr lParam, out int wheelDelta)
        {
            wheelDelta = 0;
            lock (_rawLock)
            {
                uint size = RawBufferSize;
                uint headerSize = (uint)Marshal.SizeOf(typeof(User32.RAWINPUTHEADER));
                uint written = User32.GetRawInputData(lParam, User32.RID_INPUT, _rawBuffer, ref size, headerSize);
                if (written == uint.MaxValue || written == 0) return false;

                var rawInput = Marshal.PtrToStructure<User32.RAWINPUT>(_rawBuffer);
                if (rawInput.header.dwType != User32.RIM_TYPEMOUSE) return false;
                if ((rawInput.mouse.usButtonFlags & User32.RI_MOUSE_WHEEL) != User32.RI_MOUSE_WHEEL) return false;

                wheelDelta = rawInput.mouse.usButtonData;
                return wheelDelta != 0;
            }
        }
    }
}
