using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ElsTracker.Models;
using ElsTracker.Services;

namespace ElsTracker.ViewModels;

public class MainViewModel : ObservableObject
{
    private AppData _data;
    private bool _suspendSave;
    private string _nextResetText = "";
    private string _nextResetTooltip = "";
    private string _challengeName = "";
    private string _toast = "";
    private DispatcherTimer? _toastTimer;

    public ObservableCollection<CharacterRow> Rows { get; } = new();
    public IReadOnlyList<ClassItem> Classes => ClassCatalog.All;

    public string NextResetText
    {
        get => _nextResetText;
        private set => Set(ref _nextResetText, value);
    }

    public string NextResetTooltip
    {
        get => _nextResetTooltip;
        private set => Set(ref _nextResetTooltip, value);
    }

    public string ChallengeName
    {
        get => _challengeName;
        private set => Set(ref _challengeName, value);
    }

    public string Toast
    {
        get => _toast;
        private set
        {
            if (Set(ref _toast, value))
                OnPropertyChanged(nameof(HasToast));
        }
    }
    public bool HasToast => !string.IsNullOrEmpty(_toast);

    public int SelectedCount => Rows.Count(r => r.IsSelected);
    public int TotalCount => Rows.Count;
    public bool HasSelection => SelectedCount > 0;
    public string SelectionStatus => $"{SelectedCount} selected · {TotalCount} total";

    public bool? AllSelected
    {
        get
        {
            if (Rows.Count == 0) return false;
            int sel = SelectedCount;
            if (sel == 0) return false;
            if (sel == Rows.Count) return true;
            return null;
        }
        set
        {
            if (Rows.Count == 0) { OnPropertyChanged(); return; }
            var v = value ?? false;
            foreach (var r in Rows) r.IsSelected = v;
        }
    }

    public ICommand AddCharacterCommand { get; }
    public ICommand RemoveCharacterCommand { get; }
    public ICommand CompleteSelectedCommand { get; }
    public ICommand ResetSelectedCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand MoveUpRowCommand { get; }
    public ICommand MoveDownRowCommand { get; }
    public ICommand OpenThemeCommand { get; }
    public ICommand OpenEmotesCommand { get; }
    public ICommand CopyRaidUnclearedCommand { get; }

    public event Action<CharacterRow>? RowAdded;

    public MainViewModel()
    {
        _data = DataStore.Load();

        _suspendSave = true;
        foreach (var c in _data.Characters)
            Rows.Add(WireRow(new CharacterRow(c)));
        _suspendSave = false;

        Rows.CollectionChanged += OnRowsChanged;
        ThemeService.Updated += OnThemeUpdated;

        AddCharacterCommand = new RelayCommand(_ => AddCharacter());
        RemoveCharacterCommand = new RelayCommand(p => { if (p is CharacterRow r) Rows.Remove(r); });
        CompleteSelectedCommand = new RelayCommand(_ => BulkComplete(), _ => HasSelection);
        ResetSelectedCommand = new RelayCommand(_ => BulkReset(), _ => HasSelection);
        DeleteSelectedCommand = new RelayCommand(_ => BulkDelete(), _ => HasSelection);
        OpenThemeCommand = new RelayCommand(_ => OpenFile(ThemeService.FilePath));
        OpenEmotesCommand = new RelayCommand(_ => OpenFile(EmoteService.FilePath));
        MoveUpRowCommand = new RelayCommand(p => MoveRow(p as CharacterRow, -1), p => CanMoveRow(p as CharacterRow, -1));
        MoveDownRowCommand = new RelayCommand(p => MoveRow(p as CharacterRow, 1), p => CanMoveRow(p as CharacterRow, 1));
        CopyRaidUnclearedCommand = new RelayCommand(p =>
        {
            if (p is string raid) CopyRaidUncleared(raid);
        });

        ApplyResetIfDue();
        UpdateNextResetText();
        ChallengeName = ThemeService.CurrentChallengeName;
    }

    private CharacterRow WireRow(CharacterRow row)
    {
        row.PropertyChanged += OnRowPropertyChanged;
        return row;
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (CharacterRow r in e.NewItems) r.PropertyChanged += OnRowPropertyChanged;
        if (e.OldItems != null)
            foreach (CharacterRow r in e.OldItems) r.PropertyChanged -= OnRowPropertyChanged;
        RaiseSelectionDerived();
        SyncAndSave();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CharacterRow.IconPath) ||
            e.PropertyName == nameof(CharacterRow.HasClass))
            return;

        if (e.PropertyName == nameof(CharacterRow.IsSelected))
        {
            RaiseSelectionDerived();
            return;
        }

        SyncAndSave();
    }

    private void RaiseSelectionDerived()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionStatus));
        OnPropertyChanged(nameof(AllSelected));
        CommandManager.InvalidateRequerySuggested();
    }

    private void SyncAndSave()
    {
        if (_suspendSave) return;
        _data.Characters = Rows.Select(r => r.Model).ToList();
        DataStore.Save(_data);
    }

    private bool CanMoveRow(CharacterRow? row, int offset)
    {
        if (row == null) return false;
        var index = Rows.IndexOf(row);
        return index >= 0 && index + offset >= 0 && index + offset < Rows.Count;
    }

    private void MoveRow(CharacterRow? row, int offset)
    {
        if (row == null) return;
        var index = Rows.IndexOf(row);
        if (index < 0) return;
        var newIndex = index + offset;
        if (newIndex < 0 || newIndex >= Rows.Count) return;
        Rows.Move(index, newIndex);
        CommandManager.InvalidateRequerySuggested();
    }

    private void AddCharacter()
    {
        var row = WireRow(new CharacterRow(new Character()));
        Rows.Add(row);
        RowAdded?.Invoke(row);
    }

    private void BulkComplete()
    {
        foreach (var r in Rows.Where(r => r.IsSelected).ToList())
            r.CompleteWeeklies();
    }

    private void BulkReset()
    {
        foreach (var r in Rows.Where(r => r.IsSelected).ToList())
            r.ClearWeeklies();
    }

    private void BulkDelete()
    {
        var sel = Rows.Where(r => r.IsSelected).ToList();
        if (sel.Count == 0) return;
        var msg = sel.Count == 1
            ? $"Delete \"{(string.IsNullOrWhiteSpace(sel[0].Ign) ? "(no IGN)" : sel[0].Ign)}\"?"
            : $"Delete {sel.Count} selected characters?";
        var result = MessageBox.Show(msg, "Confirm delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        foreach (var r in sel) Rows.Remove(r);
    }

    private void OpenFile(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
        }
        catch { /* ignore */ }
    }

    private static bool RaidCleared(CharacterRow r, string raid) => raid switch
    {
        "Doom"      => r.Doom || r.DoomExcluded,
        "Serp"      => r.Serp || r.SerpExcluded,
        "Abyss"     => r.Abyss || r.AbyssExcluded,
        "Challenge" => r.Challenge || r.ChallengeExcluded,
        "Atma"      => r.Atma || r.AtmaExcluded,
        "Henir"     => r.Henir || r.HenirExcluded,
        _           => true,
    };

    public void CopyRaidUncleared(string raid)
    {
        var abbrevs = new List<string>();
        var missingClass = 0;
        foreach (var row in Rows)
        {
            if (RaidCleared(row, raid)) continue;
            if (string.IsNullOrWhiteSpace(row.ClassName)) { missingClass++; continue; }
            var abbr = EmoteService.AbbrevFor(row.ClassName);
            abbrevs.Add(abbr ?? row.ClassName);
        }

        if (abbrevs.Count == 0)
        {
            ShowToast(missingClass > 0
                ? $"All {raid} cleared (skipped {missingClass} without class)"
                : $"All characters cleared {raid} ✓");
            return;
        }

        var parts = abbrevs.Select(a => $":{a}:").ToList();
        var raidEmote = raid == "Challenge"
            ? (EmoteService.ChallengeEmoteFor(ThemeService.CurrentChallengeName)
               ?? EmoteService.RaidEmoteFor(raid))
            : EmoteService.RaidEmoteFor(raid);
        if (!string.IsNullOrEmpty(raidEmote)) parts.Add($":{raidEmote}:");

        var msg = string.Join(" ", parts);
        try
        {
            Clipboard.SetText(msg);
            ShowToast($"Copied {abbrevs.Count} uncleared for {raid}" +
                (missingClass > 0 ? $" (skipped {missingClass} no-class)" : ""));
        }
        catch
        {
            ShowToast("Clipboard copy failed");
        }
    }

    private void ShowToast(string text)
    {
        Toast = text;
        _toastTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Stop();
        _toastTimer.Tick -= ToastTick;
        _toastTimer.Tick += ToastTick;
        _toastTimer.Start();
    }

    private void ToastTick(object? s, EventArgs e)
    {
        _toastTimer!.Stop();
        _toastTimer.Tick -= ToastTick;
        Toast = "";
    }

    private void OnThemeUpdated()
    {
        ChallengeName = ThemeService.CurrentChallengeName;
    }

    public void ApplyResetIfDue()
    {
        var now = DateTime.UtcNow;
        var boundary = ResetSchedule.LastBoundaryUtc(now);
        if (_data.LastResetUtc < boundary)
        {
            foreach (var r in Rows) r.ClearWeeklies();
            _data.LastResetUtc = now;
            SyncAndSave();
            ThemeService.Refresh(); // challenge color may have rotated
        }
        UpdateNextResetText();
    }

    private void UpdateNextResetText()
    {
        var nowUtc = DateTime.UtcNow;
        var nextUtc = ResetSchedule.NextBoundaryUtc(nowUtc);
        var nextLocal = nextUtc.ToLocalTime();
        var span = nextUtc - nowUtc;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        NextResetText =
            $"Next reset: {nextLocal:ddd MMM d, h:mm tt}  " +
            $"({(int)span.TotalDays}d {span.Hours}h {span.Minutes}m)";
        NextResetTooltip = $"{nextUtc:yyyy-MM-dd HH:mm} UTC · {TimeZoneInfo.Local.DisplayName}";
    }

    public void Tick()
    {
        ApplyResetIfDue();
        UpdateNextResetText();
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _can;
    public RelayCommand(Action<object?> execute, Predicate<object?>? can = null)
    { _execute = execute; _can = can; }
    public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
    public void Execute(object? p) => _execute(p);
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}
