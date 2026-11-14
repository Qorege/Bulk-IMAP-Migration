using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ImapSyncWin.Controls
{
    internal static class ControlPaintHelper
    {
        public static Color GetEffectiveParentBackColor(Control control, Color defaultColor)
        {
            if (control == null) return defaultColor;
            Control c = control.Parent;
            while (c != null && (c.BackColor == Color.Transparent || c.BackColor.A == 0))
            {
                c = c.Parent;
            }
            return (c != null && c.BackColor != Color.Transparent && c.BackColor.A > 0) ? c.BackColor : defaultColor;
        }

        public static GraphicsPath GetRoundPath(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) return path;
            radius = Math.Max(0.5f, Math.Min(radius, Math.Min(r.Width / 2f, r.Height / 2f)));
            float d = radius * 2f;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ModernProgressBar : Control
    {
        private int _total;
        private int _moved;
        private int _skipped;
        private int _errors;
        private Timer _animTimer;
        private float _animOffset;

        public int Total
        {
            get { return _total; }
            set { _total = Math.Max(1, value); Invalidate(); }
        }

        public int Moved
        {
            get { return _moved; }
            set { _moved = value; Invalidate(); }
        }

        public int Skipped
        {
            get { return _skipped; }
            set { _skipped = value; Invalidate(); }
        }

        public int Errors
        {
            get { return _errors; }
            set { _errors = value; Invalidate(); }
        }

        public bool IsAnimated { get; set; }

        public ModernProgressBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Height = 16;
            _total = 100;
            _moved = 0;
            _skipped = 0;
            _errors = 0;
            _animOffset = 0;
            IsAnimated = true;
            BackColor = Color.Transparent;

            _animTimer = new Timer();
            _animTimer.Interval = 40;
            _animTimer.Tick += (s, e) =>
            {
                if (IsAnimated && (_moved > 0 || _skipped > 0))
                {
                    _animOffset += 1.0f;
                    if (_animOffset >= 24.0f) _animOffset = 0;
                    Invalidate();
                }
            };
            _animTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_animTimer != null)
                {
                    _animTimer.Stop();
                    _animTimer.Dispose();
                    _animTimer = null;
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = ControlPaintHelper.GetEffectiveParentBackColor(this, Color.White);
            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, 0, 0, Width, Height);
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = ControlPaintHelper.GetRoundPath(rect, (Height - 1f) / 2f))
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                using (var borderPen = new Pen(Color.FromArgb(203, 213, 225), 1.0f))
                {
                    g.FillPath(bgBrush, path);
                    g.DrawPath(borderPen, path);
                }

                if (_total <= 0) return;

                float innerW = Width - 4;
                float movedWidth = (float)_moved / _total * innerW;
                float skippedWidth = (float)_skipped / _total * innerW;
                float errorsWidth = (float)_errors / _total * innerW;

                float currentX = 2;

                // Moved (Modern Sky/Blue Gradient)
                if (movedWidth > 1.0f)
                {
                    var movedRect = new RectangleF(currentX, 2, movedWidth, Height - 4);
                    using (var brush = new LinearGradientBrush(movedRect, Color.FromArgb(2, 132, 199), Color.FromArgb(14, 165, 233), LinearGradientMode.Horizontal))
                    using (var segPath = ControlPaintHelper.GetRoundPath(movedRect, Math.Min(movedWidth / 2f, (Height - 4) / 2f)))
                    {
                        g.FillPath(brush, segPath);
                    }
                    currentX += movedWidth;
                }

                // Skipped (Teal / Mint)
                if (skippedWidth > 1.0f)
                {
                    var skippedRect = new RectangleF(currentX, 2, skippedWidth, Height - 4);
                    using (var brush = new SolidBrush(Color.FromArgb(20, 184, 166)))
                    using (var segPath = ControlPaintHelper.GetRoundPath(skippedRect, Math.Min(skippedWidth / 2f, (Height - 4) / 2f)))
                    {
                        g.FillPath(brush, segPath);
                    }
                    currentX += skippedWidth;
                }

                // Errors (Coral Red)
                if (errorsWidth > 1.0f)
                {
                    var errRect = new RectangleF(currentX, 2, errorsWidth, Height - 4);
                    using (var brush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                    using (var segPath = ControlPaintHelper.GetRoundPath(errRect, Math.Min(errorsWidth / 2f, (Height - 4) / 2f)))
                    {
                        g.FillPath(brush, segPath);
                    }
                }
            }
        }
    }

    public class CardPanel : Panel
    {
        public Color BorderColor { get; set; }
        public int CornerRadius { get; set; }
        public bool DrawShadow { get; set; }

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.White;
            Padding = new Padding(16);
            BorderColor = Color.FromArgb(226, 232, 240);
            CornerRadius = 10;
            DrawShadow = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = ControlPaintHelper.GetEffectiveParentBackColor(this, Color.FromArgb(248, 250, 252));
            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, 0, 0, Width, Height);
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = ControlPaintHelper.GetRoundPath(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(BackColor))
                using (var pen = new Pen(BorderColor, 1.0f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                if (DrawShadow && Height > 4)
                {
                    using (var shadowPen = new Pen(Color.FromArgb(241, 245, 249), 1.0f))
                    {
                        g.DrawLine(shadowPen, CornerRadius, Height - 1, Width - CornerRadius, Height - 1);
                    }
                }
            }
        }
    }

    public class PillBadge : Control
    {
        public Color BadgeColor { get; set; }
        public Color TextBadgeColor { get; set; }
        public Color DotColor { get; set; }
        public bool ShowDot { get; set; }

        public PillBadge()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Height = 24;
            Width = 110;
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            BadgeColor = Color.FromArgb(241, 245, 249);
            TextBadgeColor = Color.FromArgb(71, 85, 105);
            DotColor = Color.FromArgb(148, 163, 184);
            ShowDot = true;
            BackColor = Color.Transparent;
        }

        public void SetConnected(bool connected)
        {
            if (connected)
            {
                Text = "✔ Connected";
                BadgeColor = Color.FromArgb(236, 253, 245);
                TextBadgeColor = Color.FromArgb(4, 120, 87);
                DotColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                Text = "○ Not connected";
                BadgeColor = Color.FromArgb(241, 245, 249);
                TextBadgeColor = Color.FromArgb(100, 116, 139);
                DotColor = Color.FromArgb(148, 163, 184);
            }
            Invalidate();
        }

        public void SetConnecting(string text)
        {
            Text = string.IsNullOrEmpty(text) ? "Connecting..." : text;
            BadgeColor = Color.FromArgb(240, 249, 255);
            TextBadgeColor = Color.FromArgb(2, 132, 199);
            DotColor = Color.FromArgb(148, 163, 184);
            Invalidate();
        }

        public void SetError(string text)
        {
            Text = string.IsNullOrEmpty(text) ? "Error" : text;
            BadgeColor = Color.FromArgb(254, 242, 242);
            TextBadgeColor = Color.FromArgb(185, 28, 28);
            DotColor = Color.FromArgb(239, 68, 68);
            Invalidate();
        }

        public void SetStatus(string text, Color bg, Color fg, Color dot)
        {
            Text = text;
            BadgeColor = bg;
            TextBadgeColor = fg;
            DotColor = dot;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = ControlPaintHelper.GetEffectiveParentBackColor(this, Color.White);
            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, 0, 0, Width, Height);
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float radius = (Height - 1f) / 2f;
            using (var path = ControlPaintHelper.GetRoundPath(rect, radius))
            {
                using (var brush = new SolidBrush(BadgeColor))
                using (var pen = new Pen(Color.FromArgb(
                    BadgeColor.R > 25 ? BadgeColor.R - 25 : 0,
                    BadgeColor.G > 25 ? BadgeColor.G - 25 : 0,
                    BadgeColor.B > 25 ? BadgeColor.B - 25 : 0), 1.0f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                int textLeft = 10;
                if (ShowDot)
                {
                    using (var dotBrush = new SolidBrush(DotColor))
                    {
                        g.FillEllipse(dotBrush, 8, (Height - 6) / 2, 6, 6);
                    }
                    textLeft = 18;
                }

                var textRect = new Rectangle(textLeft, 0, Width - textLeft - 6, Height);
                TextRenderer.DrawText(g, Text, Font, textRect, TextBadgeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    public enum ButtonStyleMode
    {
        Primary,
        Secondary,
        Success,
        Danger
    }

    public class ModernButton : Button
    {
        public ButtonStyleMode StyleMode { get; set; }
        public int CornerRadius { get; set; }

        private bool _isHovered;
        private bool _isPressed;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            Height = 34;
            CornerRadius = 8;
            StyleMode = ButtonStyleMode.Primary;
            BackColor = Color.Transparent;
        }

        protected override bool ShowFocusCues
        {
            get { return false; }
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { _isPressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _isPressed = false; Invalidate(); base.OnMouseUp(mevent); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = ControlPaintHelper.GetEffectiveParentBackColor(this, SystemColors.Control);
            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, 0, 0, Width, Height);
            }

            Color bgColor, fgColor, borderColor;

            switch (StyleMode)
            {
                case ButtonStyleMode.Primary:
                    bgColor = _isPressed ? Color.FromArgb(2, 105, 160) : (_isHovered ? Color.FromArgb(3, 115, 175) : Color.FromArgb(2, 132, 199));
                    fgColor = Color.White;
                    borderColor = bgColor;
                    break;
                case ButtonStyleMode.Success:
                    bgColor = _isPressed ? Color.FromArgb(15, 118, 110) : (_isHovered ? Color.FromArgb(13, 148, 136) : Color.FromArgb(20, 184, 166));
                    fgColor = Color.White;
                    borderColor = bgColor;
                    break;
                case ButtonStyleMode.Danger:
                    bgColor = _isPressed ? Color.FromArgb(185, 28, 28) : (_isHovered ? Color.FromArgb(220, 38, 38) : Color.FromArgb(239, 68, 68));
                    fgColor = Color.White;
                    borderColor = bgColor;
                    break;
                case ButtonStyleMode.Secondary:
                default:
                    bgColor = _isPressed ? Color.FromArgb(226, 232, 240) : (_isHovered ? Color.FromArgb(241, 245, 249) : Color.White);
                    fgColor = Color.FromArgb(30, 41, 59);
                    borderColor = _isHovered ? Color.FromArgb(148, 163, 184) : Color.FromArgb(203, 213, 225);
                    break;
            }

            if (!Enabled)
            {
                bgColor = Color.FromArgb(241, 245, 249);
                fgColor = Color.FromArgb(148, 163, 184);
                borderColor = Color.FromArgb(226, 232, 240);
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = ControlPaintHelper.GetRoundPath(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(bgColor))
                using (var pen = new Pen(borderColor, 1.0f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), fgColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
