// Native interop: window styles (no-activate/tool-window), cheap window moves and
// console attachment for the command-line modes.
using System;
using System.Runtime.InteropServices;

namespace BalancePet
{
    internal static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int ATTACH_PARENT_PROCESS = -1;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 2;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint ms);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint ms);

        private static uint timerPeriod;

        /// <summary>
        /// 提高系统定时器精度：默认 ~15.6ms 的调度精度会把 5ms 的帧率设置磨成 ~46ms，
        /// 想让高帧率真的丝滑就得先把精度要上来（只在进程存活期间生效）。
        /// </summary>
        public static void RaiseTimerResolution(uint ms)
        {
            if (timerPeriod == ms) return;
            if (timerPeriod != 0) TimeEndPeriod(timerPeriod);
            timerPeriod = ms;
            if (ms > 0) TimeBeginPeriod(ms);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int index);

        private const int VREFRESH = 116;

        /// <summary>
        /// 当前主显示器的刷新率（Hz）。帧率上限就取它——超过屏幕刷新率再多画也是白画。
        /// 拿不到（返回 0/1）时按 60 兜底。
        /// </summary>
        public static int RefreshRate()
        {
            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                if (dc == IntPtr.Zero) return 60;
                int hz = GetDeviceCaps(dc, VREFRESH);
                ReleaseDC(IntPtr.Zero, dc);
                if (hz <= 1) return 60;
                if (hz > 480) hz = 480;
                return hz;
            }
            catch
            {
                return 60;
            }
        }
    }
}
