using ElsTracker.Models;
using ElsTracker.Services;

namespace ElsTracker.ViewModels;

public class CharacterRow : ObservableObject
{
    private readonly Character _model;
    private bool _isSelected;

    public CharacterRow(Character model) { _model = model; }

    public Character Model => _model;
    public string Id => _model.Id;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public string ClassName
    {
        get => _model.ClassName;
        set
        {
            if (_model.ClassName == value) return;
            _model.ClassName = value ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(IconPath));
            OnPropertyChanged(nameof(HasClass));
        }
    }

    public string? IconPath => ClassCatalog.IconFor(_model.ClassName);
    public bool HasClass => !string.IsNullOrEmpty(IconPath);

    public string Ign
    {
        get => _model.Ign;
        set { if (_model.Ign != value) { _model.Ign = value ?? ""; OnPropertyChanged(); } }
    }

    public bool Doom      { get => _model.Doom;      set { if (_model.Doom      != value) { _model.Doom      = value; OnPropertyChanged(); } } }
    public bool Serp      { get => _model.Serp;      set { if (_model.Serp      != value) { _model.Serp      = value; OnPropertyChanged(); } } }
    public bool Abyss     { get => _model.Abyss;     set { if (_model.Abyss     != value) { _model.Abyss     = value; OnPropertyChanged(); } } }
    public bool Challenge { get => _model.Challenge; set { if (_model.Challenge != value) { _model.Challenge = value; OnPropertyChanged(); } } }
    public bool Atma      { get => _model.Atma;      set { if (_model.Atma      != value) { _model.Atma      = value; OnPropertyChanged(); } } }
    public bool Henir     { get => _model.Henir;     set { if (_model.Henir     != value) { _model.Henir     = value; OnPropertyChanged(); } } }

    public string Notes
    {
        get => _model.Notes;
        set { if (_model.Notes != value) { _model.Notes = value ?? ""; OnPropertyChanged(); } }
    }

    public void ClearWeeklies()
    {
        Doom = Serp = Abyss = Challenge = Atma = Henir = false;
    }

    public void CompleteWeeklies()
    {
        Doom = Serp = Abyss = Challenge = Atma = Henir = true;
    }
}
