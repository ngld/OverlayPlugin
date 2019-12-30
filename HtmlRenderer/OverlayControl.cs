using RainbowMage.HtmlRenderer;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using CefSharp;
using CefSharp.Structs;
using CefSharp.Enums;

namespace RainbowMage.HtmlRenderer
{
    public partial class OverlayControl : Control, IWinFormsTarget
    {
        private bool terminated = false;

        private object surfaceLock = new object();
        private Bitmap surfaceBuffer;
        private Bitmap popupBuffer;
        private Rectangle popupPosition;
        private bool popupVisible = false;

        public WinFormsRenderer Renderer { get; private set; }

        private string url;
        public string Url
        {
            get { return this.url; }
            set
            {
                this.url = value;

                if (this.Renderer != null)
                    this.Renderer.Load(value);
            }
        }

        private int maxFrameRate;
        public int MaxFrameRate
        {
            get
            {
                return this.maxFrameRate;
            }
            set
            {
                this.maxFrameRate = value;
                if (this.Renderer != null)
                    this.Renderer.SetMaxFramerate(value);
            }
        }

        public bool IsLoaded { get; private set; }

        public bool Locked { get; set; }

        public OverlayControl()
        {
            InitializeComponent();
        }

        public void Init(string url, int maxFrameRate = 30, object api = null)
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.Selectable | ControlStyles.UserMouse, true);
            this.SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);

            this.Renderer = new WinFormsRenderer("", url, this, api);
            this.Renderer.Locked = true;
            this.Renderer.BeginRender();

            this.MaxFrameRate = maxFrameRate;
            this.Url = url;

            this.ContextMenuStrip = new ContextMenuStrip();
            this.ContextMenuStrip.Items.Add("Reload").Click += (o, e) => this.Renderer.Reload();
            this.ContextMenuStrip.Items.Add("Open DevTools").Click += (o, e) => this.Renderer.showDevTools();

            this.Renderer.SetContextMenuCallback((int x, int y) =>
            {
                this.ContextMenuStrip.Show(this, new System.Drawing.Point() { X = x, Y = y });
                return true;
            });
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData) {
                // Override the default behavior for these keys to avoid focus loss.
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Tab:
                    Renderer.OnKeyEvent(ref msg);
                    return true;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_KEYDOWN ||
                m.Msg == NativeMethods.WM_KEYUP ||
                m.Msg == NativeMethods.WM_CHAR ||
                m.Msg == NativeMethods.WM_SYSKEYDOWN ||
                m.Msg == NativeMethods.WM_SYSKEYUP ||
                m.Msg == NativeMethods.WM_SYSCHAR)
            {
                Renderer.OnKeyEvent(ref m);
            } else
            {
                base.WndProc(ref m);
            }
        }

        public void Reload()
        {
            this.Renderer.Reload();
        }

        public void MovePopup(Rect rect)
        {
            popupPosition = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
            Invalidate();
        }

        public void SetPopupVisible(bool visible)
        {
            popupVisible = visible;
            Invalidate();
        }

        #region Render logic
        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.surfaceBuffer == null)
            {
                return;
            }

            lock (surfaceLock)
            {
                e.Graphics.DrawImage(surfaceBuffer, 0, 0);

                if (popupVisible && popupBuffer != null)
                {
                    e.Graphics.DrawImage(popupBuffer, popupPosition);
                }
            }
        }

        public void RenderFrame(PaintElementType type, Rect dirtyRect, IntPtr buffer, int width, int height)
        {
            if (!this.terminated)
            {
                try
                {
                    lock (surfaceLock)
                    {
                        Bitmap surface = type == PaintElementType.View ? surfaceBuffer : popupBuffer;
                        if (surface != null && (surface.Width != width || surface.Height != height))
                        {
                            surface.Dispose();
                            surface = null;
                        }

                        if (surface == null)
                        {
                            surface = new Bitmap(width, height);
                            if (type == PaintElementType.View)
                            {
                                surfaceBuffer = surface;
                            }
                            else
                            {
                                popupBuffer = surface;
                            }
                        }

                        var data = surface.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                        NativeMethods.CopyMemory(data.Scan0, buffer, (uint)(width * height * 4));
                        surface.UnlockBits(data);
                    }

                    var dnDirtyRect = new Rectangle(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);

                    if (type == PaintElementType.View)
                    {
                        Invalidate(new Region(dnDirtyRect));
                    } else
                    {
                        Invalidate();
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                }
            }
        }
        #endregion

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (this.surfaceBuffer != null)
            {
                this.surfaceBuffer.Dispose();
            }

            if (this.Renderer != null)
            {
                this.Renderer.Dispose();
            }

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
