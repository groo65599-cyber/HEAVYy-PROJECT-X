using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed class ControlMapperViewModel : ViewModelBase
{
    private readonly AppHost _host;

    public ControlMapperViewModel(AppHost host)
    {
        _host = host;
        AddKeyCommand = new RelayCommand(() => AddMapping(MappingInputType.Keyboard));
        AddMouseCommand = new RelayCommand(() => AddMapping(MappingInputType.MouseButton));
        RemoveCommand = new RelayCommand(RemoveMapping, () => SelectedMapping != null);
        CaptureCommand = new RelayCommand(() => StartCapture(SelectedMapping!), () => SelectedMapping != null);
        SaveCommand = new RelayCommand(SaveProfile, () => SelectedProfile != null);

        _host.Input.KeyDown += OnKeyDown;
        _host.Input.MouseButtonDown += OnMouseButtonDown;

        Reload();
    }

    public ObservableCollection<GameProfile> Profiles { get; } = new();
    public ObservableCollection<ControlMapping> Mappings { get; } = new();

    private GameProfile? _selectedProfile;
    public GameProfile? SelectedProfile
    {
        get => _selectedProfile;
        set { if (SetProperty(ref _selectedProfile, value)) ReloadMappings(); }
    }

    private ControlMapping? _selectedMapping;
    public ControlMapping? SelectedMapping
    {
        get => _selectedMapping;
        set { SetProperty(ref _selectedMapping, value); ((RelayCommand)RemoveCommand).RaiseCanExecuteChanged(); ((RelayCommand)CaptureCommand).RaiseCanExecuteChanged(); }
    }

    private ControlMapping? _capturing;
    private string _status = "Select a control point and press CAPTURE to assign a key.";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool CaptureActive => _capturing != null;

    public ICommand AddKeyCommand { get; }
    public ICommand AddMouseCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand CaptureCommand { get; }
    public ICommand SaveCommand { get; }

    private void Reload()
    {
        Profiles.Clear();
        foreach (var p in _host.Profiles.Profiles) Profiles.Add(p);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void ReloadMappings()
    {
        Mappings.Clear();
        if (SelectedProfile is null) return;
        foreach (var m in SelectedProfile.KeyboardMapping) Mappings.Add(m);
        foreach (var m in SelectedProfile.MouseMapping) Mappings.Add(m);
    }

    private void AddMapping(MappingInputType type)
    {
        if (SelectedProfile is null) return;
        var m = new ControlMapping
        {
            InputType = type,
            Input = type == MappingInputType.Keyboard ? "W" : "MouseLeft",
            Action = type == MappingInputType.Keyboard ? "New Action" : "Mouse Action"
        };
        if (type == MappingInputType.Keyboard) SelectedProfile.KeyboardMapping.Add(m);
        else SelectedProfile.MouseMapping.Add(m);
        Mappings.Add(m);
        SelectedMapping = m;
    }

    private void RemoveMapping()
    {
        if (SelectedProfile is null || SelectedMapping is null) return;
        SelectedProfile.KeyboardMapping.Remove(SelectedMapping);
        SelectedProfile.MouseMapping.Remove(SelectedMapping);
        Mappings.Remove(SelectedMapping);
        SelectedMapping = null;
    }

    private void StartCapture(ControlMapping mapping)
    {
        _capturing = mapping;
        _host.Input.CaptureEnabled = true;
        Status = "Press any key or mouse button to assign...";
        OnPropertyChanged(nameof(CaptureActive));
    }

    private void OnKeyDown(string key)
    {
        if (_capturing is null) return;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _capturing.Input = key;
            _capturing.InputType = MappingInputType.Keyboard;
            FinishCapture();
        });
    }

    private void OnMouseButtonDown(string button)
    {
        if (_capturing is null) return;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _capturing.Input = button;
            _capturing.InputType = MappingInputType.MouseButton;
            FinishCapture();
        });
    }

    private void FinishCapture()
    {
        _capturing = null;
        _host.Input.CaptureEnabled = false;
        Status = "Mapping updated. Remember to SAVE.";
        OnPropertyChanged(nameof(CaptureActive));

        // ControlMapping is a plain model, so re-insert it to force the list and
        // the visual editor to refresh the changed input name.
        if (SelectedMapping is { } sel)
        {
            int idx = Mappings.IndexOf(sel);
            if (idx >= 0)
            {
                Mappings.RemoveAt(idx);
                Mappings.Insert(idx, sel);
            }
            SelectedMapping = sel;
        }
    }

    private void SaveProfile()
    {
        if (SelectedProfile is null) return;
        _host.Profiles.Save(SelectedProfile);
        Status = "Profile saved.";
    }
}
