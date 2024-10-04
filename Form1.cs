using System.Runtime.InteropServices;
using Karna.Magnification;
using System.Windows.Forms;
using System.Text;
using System.Diagnostics;

namespace WindowOverlayApp
{
    public partial class Form1 : Form
    {
        const int SWP_NOACTIVATE = 0x0010;
        const int SWP_SHOWWINDOW = 0x0040;
        const int SWP_NOMOVE = 0x0002;
        const int SWP_NOSIZE = 0x0001;

        // Window event constants
        const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        const uint EVENT_SYSTEM_FOREGROUND = 0x0003; // Triggers when a window comes to the foreground
        const uint WINEVENT_OUTOFCONTEXT = 0x0000;


        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
                                             uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        const int GW_HWNDFIRST = 0;
        const int GW_HWNDLAST = 1;
        const int GW_HWNDNEXT = 2;
        const int GW_HWNDPREV = 3;
        const int GW_OWNER = 4;
        const int GW_CHILD = 5;
        const int GW_ENABLEDPOPUP = 6;

        // Delegate for WinEvent hook
        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private IntPtr notepadHandle = IntPtr.Zero;
        private IntPtr winEventHook = IntPtr.Zero;
        private IntPtr foregroundEventHook = IntPtr.Zero;
        private WinEventDelegate winEventDelegate, foregroundEventDelegate;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        Magnifier magnifier;
        private System.Windows.Forms.Timer resizeTimer;
        private bool isResizing = false;

        public Form1()
        {
            InitializeComponent();

            winEventDelegate = new WinEventDelegate(WinEventProc);
            foregroundEventDelegate = new WinEventDelegate(ForegroundEventProc);

            magnifier = new Magnifier(this);
        }

        const int WS_EX_TOOLWINDOW = 0x00000080;  // Hides from Alt+Tab
        const int WS_EX_APPWINDOW = 0x00040000;   // Forces a window to appear in Alt+Tab
        const int WS_EX_NOACTIVATE = 0x08000000;  // Prevents the window from receiving focus
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_LAYERED = 0x80000;
                const int WS_EX_TRANSPARENT = 0x20;
                CreateParams cp = base.CreateParams;
                // Hide the window from Alt+Tab and taskbar by using WS_EX_TOOLWINDOW
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle |= WS_EX_LAYERED;
                cp.ExStyle |= WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Find the Notepad window by its title (change this if your Notepad title is different)
            notepadHandle = FindWindow(null, "Vivado 2024.1");

            if (notepadHandle != IntPtr.Zero)
            {
                // Hook into the window events to track movements, resizes, and other changes
                winEventHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
                foregroundEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, foregroundEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
                UpdateOverlayWindow(); // Update position and size at load
            }
            else
            {
                MessageBox.Show("Notepad window not found.");
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            // Unhook the event when closing the form
            if (winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(winEventHook);
            }
        }

        private RECT lastRect; // To store the last known rectangle

        private void UpdateOverlayWindow()
        {
            SetWindowPos(this.Handle, -1, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE);
            
            if (notepadHandle == IntPtr.Zero)
                return;

            // Get the position and size of the Notepad window
            RECT rect = new RECT();
            if (GetWindowRect(notepadHandle, ref rect))
            {
                // Adjust for window borders in Aero
                const int windowBorder = 2;
                const int aeroBorder = 7 + windowBorder;
                const int aeroBorderTop = -1 + windowBorder;

                rect.Left += aeroBorder;
                rect.Top += aeroBorderTop;
                rect.Right -= aeroBorder;
                rect.Bottom -= aeroBorder;

                // Set this form's size and position to match Notepad's
                this.Size = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
                this.Location = new Point(rect.Left, rect.Top);

                // Overlay this window on top of Notepad's window

                //magnifier.ResizeMagnifier();
                //SetWindowPos(this.Handle, GetWindow(notepadHandle, GW_OWNER), 0, 0, 0, 0, SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE);
                SetWindowPos(this.Handle, -1, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE);
                magnifier.UpdateMaginifier();
            }
        }


        // This method is called whenever the Notepad window changes position or size
        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd == notepadHandle)
            {
                UpdateOverlayWindow(); // Adjust overlay window whenever the target window moves or resizes
            }
        }

        private void ForegroundEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (IsAnyParentMatching(hwnd, notepadHandle))
            {
                UpdateOverlayWindow();
            } else if (!IsWindowClass(hwnd, "ForegroundStaging") && !IsWindowClass(hwnd, "MultitaskingViewFrame"))
            {
                SetWindowPos(this.Handle, 1, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);

            }
        }

        static bool IsAnyParentMatching(IntPtr hwnd, IntPtr targetHwnd)
        {
            IntPtr currentHwnd = hwnd;

            while (currentHwnd != IntPtr.Zero)
            {
                if (currentHwnd == targetHwnd)
                    return true;

                currentHwnd = GetParent(currentHwnd);
            }

            return false;
        }


        static bool IsWindowClass(IntPtr hWnd, string className)
        {
            StringBuilder wClassName = new StringBuilder(className.Length+20);
            GetClassName(hWnd, wClassName, className.Length+20);
            Debug.WriteLine(wClassName);
            return wClassName.ToString() == className;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
