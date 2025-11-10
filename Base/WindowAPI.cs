using System.Runtime.InteropServices;

namespace VendingMachineTest.Base
{
    public static class WindowAPI
    {
        public const int CT_SHUTDOWN_COMMAND = 1079;
        public const int CT_UPDATE_COMMAND = 1080;
        public const int CT_RESET_COMMAND = 1081;
        public const int CT_SEND_COMMAND = 1082;
        public const int CT_SEND_RESETTIME_COMMAND = 1083;

        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        public struct DataStruct
        {
            public int mDataType;
            [MarshalAs(UnmanagedType.LPStr)]
            public string? mStatusData;
        }

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(
          IntPtr hWnd,
          int Msg,
          IntPtr wParam,
          IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessage(
          IntPtr hWnd,
          uint Msg,
          IntPtr wParam,
          ref COPYDATASTRUCT lParam);

        public static void SendMessageWndProc(IntPtr targetHWnd, int iType, string? strRxHex, IntPtr mainHWnd)
        {
            PostMessage(targetHWnd, iType, mainHWnd, new IntPtr());
        }
    }
}
