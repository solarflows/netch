namespace Netch.Forms;

public class SyncGlobalCheckBox : CheckBox
{
    public SyncGlobalCheckBox()
    {
        AutoCheck = false;
        OnSyncGlobalChanged();
    }

    private bool _syncGlobal;

    private bool _globalValue;

    public bool SyncGlobal
    {
        get => _syncGlobal;
        set
        {
            if (value == _syncGlobal)
                return;

            _syncGlobal = value;

            OnSyncGlobalChanged();
        }
    }

    public bool GlobalValue
    {
        get => _globalValue;
        set
        {
            if (value == _globalValue)
                return;

            _globalValue = value;

            if (SyncGlobal)
                Checked = value;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        if (Checked == GlobalValue)
        {
            SyncGlobal = !SyncGlobal;
            if (SyncGlobal)
                return;
        }

        Checked = !Checked;
        base.OnClick(e);
    }

    public bool? Value
    {
        get => _syncGlobal ? null : Checked;
        set
        {
            if (value == null)
            {
                SyncGlobal = true;
            }
            else
            {
                SyncGlobal = false;
                Checked = (bool)value;
            }
        }
    }

    private void OnSyncGlobalChanged()
    {
        bool dark = Services.ThemeService.IsDarkMode;
        if (_syncGlobal)
        {
            Font = new Font(Font, FontStyle.Regular);
            ForeColor = dark ? Services.ThemeService.DarkTextDim : SystemColors.ControlText;
            BackColor = Color.Transparent;
        }
        else
        {
            Font = new Font(Font, FontStyle.Bold);
            ForeColor = dark ? Color.FromArgb(100, 185, 255) : Color.FromArgb(0, 102, 204);
            BackColor = Color.Transparent;
        }
    }
}