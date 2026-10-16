using Netch.Controllers;
using Netch.Interfaces;
using Netch.Models;
using Netch.Models.Modes;
using Netch.Utils;

namespace Netch.Services;

public class ModeService
{
    public static readonly ModeService Instance = new();

    /// <summary>Raised when a mode is deleted, allowing UI components to update without direct coupling.</summary>
    public static event EventHandler<Mode>? ModeDeleted;

    public static string ModeDirectoryFullName => Path.Combine(Global.NetchDir, "mode");

    public static string GetRelativePath(string fullName)
    {
        var length = ModeDirectoryFullName.Length;
        if (!ModeDirectoryFullName.EndsWith("\\"))
            length++;

        return fullName[length..];
    }

    public static string GetFullPath(string relativeName)
    {
        return Path.Combine(ModeDirectoryFullName, relativeName);
    }

    public void Load()
    {
        Global.Modes.Clear();
        LoadCore(ModeDirectoryFullName);
        Sort();
        Log.Information("Loaded {Count} modes from {Directory}", Global.Modes.Count, ModeDirectoryFullName);
        Global.MainForm.LoadModes();
    }

    private void LoadCore(string modeDirectory)
    {
        foreach (var directory in Directory.GetDirectories(modeDirectory))
            LoadCore(directory);

        // skip Directory with a disabled file in
        if (File.Exists(Path.Combine(modeDirectory, Constants.DisableModeDirectoryFileName)))
            return;

        foreach (var file in Directory.GetFiles(modeDirectory))
        {
            try
            {
                Global.Modes.Add(ModeHelper.LoadMode(file));
            }
            catch (NotSupportedException)
            {
                // ignored
            }
            catch (Exception e)
            {
                Log.Warning(e, "Load mode \"{FileName}\" failed", file);
            }
        }
    }

    public static void Add(Mode mode)
    {
        if (mode.FullName == null)
            throw new InvalidOperationException();

        Global.Modes.Add(mode);
        Sort();

        mode.WriteFile();
    }

    public static void Sort()
    {
        // TODO: discuss better sort criteria and replace Mode Collection type
        Global.Modes.Sort((a, b) => string.Compare(a.i18NRemark, b.i18NRemark, StringComparison.Ordinal));
        Global.MainForm.LoadModes();
    }

    public static void Delete(Mode mode)
    {
        if (mode.FullName == null)
            throw new ArgumentException(nameof(mode.FullName));

        Global.Modes.Remove(mode);
        Global.MainForm.ModeComboBox.Items.Remove(mode);
        ModeDeleted?.Invoke(null, mode);

        if (File.Exists(mode.FullName))
            File.Delete(mode.FullName);
    }

    public static IModeController GetModeControllerByType(ModeType type, out ushort? port, out string portName)
    {
        port = null;
        portName = string.Empty;
        switch (type)
        {
            case ModeType.ProcessMode:
                return new NFController();
            case ModeType.TunMode:
                return new TUNController();
            case ModeType.ShareMode:
                return new PcapController();
            default:
                Log.Error("Unknown Mode Type \"{Type}\"", (int)type);
                throw new MessageException("Unknown Mode Type");
        }
    }
}