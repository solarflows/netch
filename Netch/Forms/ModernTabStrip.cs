using System.ComponentModel;
using Netch.Services;

namespace Netch.Forms;

[DesignerCategory(@"Code")]
public class ModernTabStrip : Control
{
    private TabControl? _tabControl;
    private int _hoverIndex = -1;
    private readonly List<Rectangle> _tabRects = new();

    public ModernTabStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        Height = 36;
        DoubleBuffered = true;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TabControl? TargetTabControl
    {
        get => _tabControl;
        set
        {
            if (_tabControl == value)
                return;

            if (_tabControl != null)
            {
                _tabControl.SelectedIndexChanged -= TabControl_SelectedIndexChanged;
                _tabControl.ControlAdded -= TabControl_ControlChanged;
                _tabControl.ControlRemoved -= TabControl_ControlChanged;
            }

            _tabControl = value;

            if (_tabControl != null)
            {
                _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
                _tabControl.ControlAdded += TabControl_ControlChanged;
                _tabControl.ControlRemoved += TabControl_ControlChanged;
            }

            Invalidate();
        }
    }

    private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        Invalidate();
    }

    private void TabControl_ControlChanged(object? sender, ControlEventArgs e)
    {
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        bool isDark = ThemeService.IsDarkMode;
        Color bgColor = isDark ? ThemeService.DarkBg : (Parent?.BackColor ?? SystemColors.Control);

        using (var bgBrush = new SolidBrush(bgColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        _tabRects.Clear();

        if (_tabControl == null || _tabControl.TabCount == 0)
            return;

        int currentX = 4;
        int tabHeight = Height - 2;

        for (int i = 0; i < _tabControl.TabCount; i++)
        {
            var page = _tabControl.TabPages[i];
            bool isSelected = _tabControl.SelectedIndex == i;
            bool isHover = _hoverIndex == i && !isSelected;

            using var textFont = isSelected
                ? new Font(Font, FontStyle.Bold)
                : new Font(Font, FontStyle.Regular);

            var textSize = TextRenderer.MeasureText(page.Text, textFont);
            int tabWidth = Math.Max(textSize.Width + 24, 48);
            var rect = new Rectangle(currentX, 1, tabWidth, tabHeight);
            _tabRects.Add(rect);

            if (isHover)
            {
                var hoverColor = isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(15, 0, 0, 0);
                using var hoverBrush = new SolidBrush(hoverColor);
                g.FillRectangle(hoverBrush, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4));
            }

            Color textColor = isDark
                ? (isSelected ? ThemeService.DarkText : ThemeService.DarkTextDim)
                : (isSelected ? Color.FromArgb(0, 102, 204) : Color.FromArgb(80, 80, 80));

            TextRenderer.DrawText(g,
                page.Text,
                textFont,
                rect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (isSelected)
            {
                var accentColor = isDark ? Color.FromArgb(0, 120, 215) : Color.FromArgb(0, 102, 204);
                using var accentBrush = new SolidBrush(accentColor);
                var barRect = new Rectangle(rect.X + 4, Height - 3, rect.Width - 8, 3);
                g.FillRectangle(accentBrush, barRect);
            }

            currentX += tabWidth + 2;
        }

        // 底部基准微线 (1px 平齐细线)
        Color dividerColor = isDark ? Color.FromArgb(48, 48, 48) : Color.FromArgb(220, 220, 220);
        using var dividerPen = new Pen(dividerColor, 1);
        g.DrawLine(dividerPen, 0, Height - 1, Width, Height - 1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_tabControl == null || e.Button != MouseButtons.Left)
            return;

        for (int i = 0; i < _tabRects.Count; i++)
        {
            if (_tabRects[i].Contains(e.Location))
            {
                if (_tabControl.SelectedIndex != i)
                {
                    _tabControl.SelectedIndex = i;
                    Invalidate();
                }
                break;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int newHover = -1;
        for (int i = 0; i < _tabRects.Count; i++)
        {
            if (_tabRects[i].Contains(e.Location))
            {
                newHover = i;
                break;
            }
        }

        if (newHover != _hoverIndex)
        {
            _hoverIndex = newHover;
            Cursor = _hoverIndex != -1 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }
}

[DesignerCategory(@"Code")]
public class BorderlessTabControl : TabControl
{
    public BorderlessTabControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Appearance = TabAppearance.FlatButtons;
        ItemSize = new Size(0, 1);
        SizeMode = TabSizeMode.Fixed;
    }

    protected override void WndProc(ref Message m)
    {
        // 0x1328 is TCM_ADJUSTRECT
        if (m.Msg == 0x1328 && !DesignMode)
        {
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);
    }
}
