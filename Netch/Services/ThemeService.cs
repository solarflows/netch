using System.Runtime.InteropServices;
using Microsoft.Win32;
using Netch.Utils;

namespace Netch.Services;

public static class ThemeService
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
    public static readonly Color DarkCard = Color.FromArgb(45, 45, 45);
    public static readonly Color DarkInput = Color.FromArgb(55, 55, 55);
    public static readonly Color DarkBorder = Color.FromArgb(75, 75, 75);
    public static readonly Color DarkText = Color.FromArgb(240, 240, 240);
    public static readonly Color DarkTextDim = Color.FromArgb(170, 170, 170);

    private static readonly DarkThemeColorTable DarkColors = new();
    private static readonly ToolStripRenderer DarkRenderer = new ToolStripProfessionalRenderer(DarkColors);
    private static readonly ToolStripRenderer DefaultRenderer = new ToolStripProfessionalRenderer();

    static ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (string.Equals(Global.Settings.Theme, "System", StringComparison.OrdinalIgnoreCase))
            {
                ApplyToOpenForms();
            }
        };
    }

    public static bool IsDarkMode
    {
        get
        {
            var theme = Global.Settings.Theme;
            if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
                return false;

            return IsSystemDarkMode();
        }
    }

    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int lightMode)
                return lightMode == 0;
        }
        catch
        {
            // ignored
        }

        return false;
    }

    public static void SetWindowDarkMode(IntPtr hWnd, bool dark)
    {
        if (hWnd == IntPtr.Zero)
            return;

        int useDarkMode = dark ? 1 : 0;
        int res = DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        if (res != 0)
        {
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
        }
    }

    public static void ApplyToOpenForms()
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
        {
            if (form != null && !form.IsDisposed)
            {
                Apply(form);
            }
        }
    }

    public static void Apply(Form form)
    {
        bool dark = IsDarkMode;
        SetWindowDarkMode(form.Handle, dark);

        form.BackColor = dark ? DarkBg : SystemColors.Control;
        form.ForeColor = dark ? DarkText : SystemColors.ControlText;

        ApplyToControls(form.Controls, dark);
    }

    private static void ApplyToControls(Control.ControlCollection controls, bool dark)
    {
        foreach (Control c in controls)
        {
            switch (c)
            {
                case TabControl tc:
                    tc.BackColor = dark ? DarkBg : SystemColors.Control;
                    tc.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    foreach (TabPage page in tc.TabPages)
                    {
                        page.BackColor = dark ? DarkCard : SystemColors.ButtonFace;
                        page.ForeColor = dark ? DarkText : SystemColors.ControlText;
                        ApplyToControls(page.Controls, dark);
                    }
                    break;

                case GroupBox gb:
                    gb.BackColor = dark ? DarkCard : Color.Transparent;
                    gb.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    ApplyToControls(gb.Controls, dark);
                    break;

                case Panel p:
                    p.BackColor = dark ? DarkCard : Color.Transparent;
                    p.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    ApplyToControls(p.Controls, dark);
                    break;

                case Button btn:
                    btn.BackColor = dark ? DarkInput : SystemColors.Control;
                    btn.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    btn.FlatStyle = FlatStyle.Standard;
                    break;

                case TextBox tb:
                    tb.BackColor = dark ? DarkInput : SystemColors.Window;
                    tb.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    break;

                case ComboBox cb:
                    cb.BackColor = dark ? DarkInput : SystemColors.Window;
                    cb.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    break;

                case MenuStrip ms:
                    ms.BackColor = dark ? DarkBg : SystemColors.Control;
                    ms.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    ms.Renderer = dark ? DarkRenderer : DefaultRenderer;
                    break;

                case StatusStrip ss:
                    ss.BackColor = dark ? DarkBg : SystemColors.Control;
                    ss.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    ss.Renderer = dark ? DarkRenderer : DefaultRenderer;
                    break;

                case ContextMenuStrip cms:
                    cms.BackColor = dark ? DarkCard : SystemColors.Control;
                    cms.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    cms.Renderer = dark ? DarkRenderer : DefaultRenderer;
                    break;

                default:
                    c.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    if (c.HasChildren)
                    {
                        ApplyToControls(c.Controls, dark);
                    }
                    break;
            }
        }
    }

    private class DarkThemeColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => DarkBg;
        public override Color MenuStripGradientEnd => DarkBg;
        public override Color ToolStripDropDownBackground => DarkCard;
        public override Color ImageMarginGradientBegin => DarkCard;
        public override Color ImageMarginGradientMiddle => DarkCard;
        public override Color ImageMarginGradientEnd => DarkCard;
        public override Color MenuBorder => DarkBorder;
        public override Color MenuItemBorder => DarkBorder;
        public override Color MenuItemSelected => DarkInput;
        public override Color MenuItemSelectedGradientBegin => DarkInput;
        public override Color MenuItemSelectedGradientEnd => DarkInput;
        public override Color MenuItemPressedGradientBegin => DarkCard;
        public override Color MenuItemPressedGradientEnd => DarkCard;
        public override Color StatusStripGradientBegin => DarkBg;
        public override Color StatusStripGradientEnd => DarkBg;
    }
}
