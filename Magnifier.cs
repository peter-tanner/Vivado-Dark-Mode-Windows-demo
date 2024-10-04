using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Karna.Magnification
{
    public class Magnifier : IDisposable
    {
        private Form form;
        private IntPtr hwndMag;
        private float magnification;
        private bool initialized;
        private RECT magWindowRect = new RECT();
        private System.Windows.Forms.Timer timer;
        public nint afterHwnd;

        public Magnifier(Form form)
        {
            if (form == null)
                throw new ArgumentNullException("form");

            magnification = 1.0f;
            this.form = form;
            this.form.Resize += new EventHandler(form_Resize);
            this.form.FormClosing += new FormClosingEventHandler(form_FormClosing);

            timer = new System.Windows.Forms.Timer();
            timer.Tick += new EventHandler(timer_Tick);

            initialized = NativeMethods.MagInitialize();
            if (initialized)
            {
                SetupMagnifier();
                timer.Interval = NativeMethods.USER_TIMER_MINIMUM;
                timer.Enabled = true;
            }
        }

        void form_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer.Enabled = false;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            UpdateMaginifier();
        }

        void form_Resize(object sender, EventArgs e)
        {
            ResizeMagnifier();
        }

        ~Magnifier()
        {
            Dispose(false);
        }

        public virtual void ResizeMagnifier()
        {
            if ( initialized && (hwndMag != IntPtr.Zero))
            {
                NativeMethods.GetClientRect(form.Handle, ref magWindowRect);
                // Resize the control to fill the window.
                NativeMethods.SetWindowPos(hwndMag, IntPtr.Zero,
                    magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, 0);
            }
        }

        public virtual void UpdateMaginifier()
        {
            if ((!initialized) || (hwndMag == IntPtr.Zero))
                return;

            RECT sourceRect = new RECT();

            sourceRect.left = form.Left;
            sourceRect.right = form.Right;
            sourceRect.top = form.Top;
            sourceRect.bottom = form.Bottom;

            if (this.form == null || this.form.IsDisposed)
            {
                timer.Enabled = false;
                return;
            }

            // Set the source rectangle for the magnifier control.
            NativeMethods.MagSetWindowSource(hwndMag, sourceRect);
            // Reclaim topmost status, to prevent unmagnified menus from remaining in view. 
            //NativeMethods.SetWindowPos(hwndMag, form.Handle, 0, 0, 0, 0,
            //    (int)SetWindowPosFlags.SWP_NOACTIVATE | (int)SetWindowPosFlags.SWP_NOMOVE | (int)SetWindowPosFlags.SWP_NOSIZE);

            // Force redraw.
            redraw();
        }

        public virtual void redraw()
        {
            NativeMethods.InvalidateRect(hwndMag, IntPtr.Zero, true);
        }

        public float Magnification
        {
            get { return magnification; }
            set
            {
                if (magnification != value)
                {
                    magnification = value;
                    // Set the magnification factor.
                    Transformation matrix = new Transformation(magnification);
                    NativeMethods.MagSetWindowTransform(hwndMag, ref matrix);
                }
            }
        }

        protected void SetupMagnifier()
        {
            if (!initialized)
                return;

            IntPtr hInst;

            hInst = NativeMethods.GetModuleHandle(null);

            // Make the window opaque.
            form.AllowTransparency = true;
            form.TransparencyKey = Color.Empty;
            form.Opacity = 255;

            // Create a magnifier control that fills the client area.
            NativeMethods.GetClientRect(form.Handle, ref magWindowRect);
            hwndMag = NativeMethods.CreateWindow(
                (int)ExtendedWindowStyles.WS_EX_TRANSPARENT | (int)ExtendedWindowStyles.WS_EX_NOACTIVATE, 
                NativeMethods.WC_MAGNIFIER,
                "MagnifierWindow", 
                (int)WindowStyles.WS_CHILD | (int)MagnifierStyle.MS_INVERTCOLORS |
                (int)WindowStyles.WS_VISIBLE,
                magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom,
                form.Handle, IntPtr.Zero, hInst, IntPtr.Zero
            );

            if (hwndMag == IntPtr.Zero)
            {
                return;
            }

            // Set the magnification factor.
            Transformation matrix = new Transformation(magnification);
            NativeMethods.MagSetWindowTransform(hwndMag, ref matrix);
        }

        public bool SetFilter(nint hwnd)
        {
            bool res = true;
            //IntPtr filterHandle = Marshal.AllocHGlobal(sizeof(int));

            //try
            //{
            //    Marshal.WriteInt64(filterHandle, (nint)hwnd); // Store the integer value
            //    res &= NativeMethods.MagSetWindowFilterList(hwndMag, (int)FilterMode.MW_FILTERMODE_INCLUDE, 1, filterHandle);
            //    res &= NativeMethods.MagGetWindowFilterList(hwndMag, filterHandle, 0, filterHandle) == 1;
            //}
            //finally
            //{
            //    // Free the allocated unmanaged memory to prevent memory leaks
            //    Marshal.FreeHGlobal(filterHandle);
            //}
            return res;
        }

        protected void RemoveMagnifier()
        {
            if (initialized)
                NativeMethods.MagUninitialize();
        }

        protected virtual void Dispose(bool disposing)
        {
            timer.Enabled = false;
            if (disposing)
                timer.Dispose();
            timer = null;
            form.Resize -= form_Resize;
            RemoveMagnifier();
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
