using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;
using Microsoft.Win32;

namespace HPXPirate.ViewModels;

public sealed class GameProfilesViewModel : ViewModelBase
{
    private readonly AppHost _host;

    public GameProfilesViewModel(AppHost host)
    {
        _host = host;
        NewCommand = new RelayCommand(NewProfile);
        SaveCommand = new RelayCommand(SaveProfile, () => SelectedProfile != null);
        DeleteCommand = new RelayCommand(DeleteProfile, () => SelectedProfile != null);
        DuplicateCommand = new RelayCommand(DuplicateProfile, () => SelectedProfile != null);
        ResetCommand = new RelayCommand(ResetProfile, () => SelectedProfile != null);
        ApplyCommand = new RelayCommand(ApplyProfile, () => SelectedProfile != null);
        ImportCommand = new RelayCommand(ImportProfile);
        ExportCommand = new RelayCommand(ExportProfile, () => SelectedProfile != null);
        BackupCommand = new RelayCommand(BackupProfile, () => SelectedProfile != null);
        RestoreCommand = new RelayCommand(RestoreProfile);
        Reload();
    }

    public ObservableCollection<GameProfile> Profiles { get; } = new();

    private GameProfile? _selected;
    public GameProfile? SelectedProfile
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        }
    }

    private string _status = "";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool AutoLoad
    {
        get => _host.AppSettings.AutoLoadProfiles;
        set { _host.AppSettings.AutoLoadProfiles = value; OnPropertyChanged(); _host.Settings.Save(); }
    }

    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }

    private void Reload()
    {
        Profiles.Clear();
        foreach (var p in _host.Profiles.Profiles)
            Profiles.Add(p);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void NewProfile()
    {
        var p = GameProfile.CreateDefault("New Game");
        _host.Profiles.Save(p);
        Profiles.Add(p);
        SelectedProfile = p;
        Status = "New profile created.";
    }

    private void SaveProfile()
    {
        if (SelectedProfile is null) return;
        _host.Profiles.Save(SelectedProfile);
        Status = $"Saved '{SelectedProfile.Game}'.";
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null) return;
        var name = SelectedProfile.Game;
        _host.Profiles.Delete(SelectedProfile);
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
        Status = $"Deleted '{name}'.";
    }

    private void DuplicateProfile()
    {
        if (SelectedProfile is null) return;
        var json = JsonHelper.Serialize(SelectedProfile);
        var copy = JsonHelper.Deserialize<GameProfile>(json)!;
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Game = SelectedProfile.Game + " (copy)";
        _host.Profiles.Save(copy);
        Profiles.Add(copy);
        SelectedProfile = copy;
        Status = "Profile duplicated.";
    }

    private void ResetProfile()
    {
        if (SelectedProfile is null) return;
        var fresh = _host.Profiles.Reset(SelectedProfile);
        int idx = Profiles.IndexOf(SelectedProfile);
        if (idx >= 0) Profiles[idx] = fresh;
        SelectedProfile = fresh;
        Status = "Profile reset to defaults.";
    }

    private void ApplyProfile()
    {
        if (SelectedProfile is null) return;
        var p = SelectedProfile;
        var s = _host.AppSettings;
        s.Mouse = p.Mouse;
        s.DragAssist = p.DragAssist;
        s.EasyDrag = p.EasyDrag;
        s.Crosshair = p.Crosshair;
        s.DefaultBoostMode = p.PerformanceMode;
        _host.Settings.Save();
        _host.Crosshair.ApplySettings(s.Crosshair);
        Status = $"Applied profile '{p.Game}'.";
    }

    private void ImportProfile()
    {
        var dlg = new OpenFileDialog { Filter = "HPX Profile (*.json)|*.json|All files (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var p = _host.Profiles.Import(dlg.FileName);
            Profiles.Add(p);
            SelectedProfile = p;
            Status = $"Imported '{p.Game}'.";
        }
        catch (Exception ex) { Status = "Import failed: " + ex.Message; }
    }

    private void ExportProfile()
    {
        if (SelectedProfile is null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "HPX Profile (*.json)|*.json",
            FileName = SelectedProfile.Game.Replace(' ', '_') + ".json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _host.Profiles.Export(SelectedProfile, dlg.FileName);
            Status = "Exported to " + dlg.FileName;
        }
        catch (Exception ex) { Status = "Export failed: " + ex.Message; }
    }

    private void BackupProfile()
    {
        if (SelectedProfile is null) return;
        try
        {
            var path = _host.Profiles.Backup(SelectedProfile);
            Status = "Backup created: " + path;
        }
        catch (Exception ex) { Status = "Backup failed: " + ex.Message; }
    }

    private void RestoreProfile()
    {
        var dlg = new OpenFileDialog { Filter = "HPX Profile Backup (*.json)|*.json|All files (*.*)|*.*", InitialDirectory = AppPaths.Backups };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var p = _host.Profiles.Restore(dlg.FileName);
            if (!Profiles.Contains(p)) Profiles.Add(p);
            SelectedProfile = p;
            Status = $"Restored '{p.Game}'.";
        }
        catch (Exception ex) { Status = "Restore failed: " + ex.Message; }
    }
}
