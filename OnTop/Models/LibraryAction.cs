namespace OnTop.Models;

/// <summary>What a library action does. INT-serialized, append-only.</summary>
public enum LibraryKind
{
    RunCommand = 0, KillProcess = 1, ToggleWindow = 2, RunMacro = 3, ApplyPlacement = 4,
    SwitchProfile = 5, PauseToggle = 6, Peek = 7, StealthToggle = 8, PinUnderCursor = 9,
    ApplyScene = 10,
}

/// <summary>
/// S28 Stage F — a named, reusable action (ROADMAP "Action Library"). It can be
/// bound to a HOTKEY and/or assigned to ZONES (ZoneAction.Library) — the same
/// decoupling macros already have. Every kind dispatches to an EXISTING
/// choke-point (MainViewModel.RunLibraryAction) — the library adds no new
/// power, only new ways to reach existing, already-guarded verbs. Inert while
/// Cfg.ActionLibraryEnabled is off (no hotkeys bind, zones refuse).
/// </summary>
public class LibraryAction : ObservableObject
{
    private string _id = System.Guid.NewGuid().ToString("N")[..8];
    private string _name = "Action";
    private bool _enabled = true;
    private LibraryKind _kind = LibraryKind.Peek;
    private string _command = "", _process = "", _macroId = "", _placementId = "", _profileName = "", _hotkey = "";

    public string Id { get => _id; set => Set(ref _id, value ?? ""); }
    public string Name { get => _name; set => Set(ref _name, value ?? ""); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public LibraryKind Kind { get => _kind; set => Set(ref _kind, value); }
    /// <summary>RunCommand payload (verbatim; via the single launcher).</summary>
    public string Command { get => _command; set => Set(ref _command, value ?? ""); }
    /// <summary>KillProcess / ToggleWindow payload.</summary>
    public string Process { get => _process; set => Set(ref _process, value ?? ""); }
    public string MacroId { get => _macroId; set => Set(ref _macroId, value ?? ""); }
    public string PlacementId { get => _placementId; set => Set(ref _placementId, value ?? ""); }
    public string ProfileName { get => _profileName; set => Set(ref _profileName, value ?? ""); }
    /// <summary>Optional global hotkey (conflict-checked, one combo one feature).</summary>
    public string Hotkey { get => _hotkey; set => Set(ref _hotkey, value ?? ""); }

    /// <summary>Human summary of the payload, for grids/menus (verbatim).</summary>
    public string Summary => Kind switch
    {
        LibraryKind.RunCommand => $"Run: {Command}",
        LibraryKind.KillProcess => $"Kill: {Process}",
        LibraryKind.ToggleWindow => $"Toggle window: {Process}",
        LibraryKind.RunMacro => $"Macro id {MacroId}",
        LibraryKind.ApplyPlacement => $"Placement id {PlacementId}",
        LibraryKind.SwitchProfile => $"Profile: {ProfileName}",
        LibraryKind.PauseToggle => "Pause / resume detection",
        LibraryKind.Peek => "Peek at all zones",
        LibraryKind.StealthToggle => "Toggle Stealth",
        LibraryKind.PinUnderCursor => "Pin/unpin window under cursor",
        LibraryKind.ApplyScene => $"Scene id {PlacementId}",
        _ => Kind.ToString(),
    };
    /// <summary>Kinds that launch, kill, or synthesize input — quarantined on import.</summary>
    public bool IsPowerful => Kind is LibraryKind.RunCommand or LibraryKind.KillProcess or LibraryKind.RunMacro or LibraryKind.ApplyPlacement;

    public LibraryAction Clone() => new()
    {
        Id = Id, Name = Name, Enabled = Enabled, Kind = Kind, Command = Command, Process = Process,
        MacroId = MacroId, PlacementId = PlacementId, ProfileName = ProfileName, Hotkey = Hotkey,
    };
}
