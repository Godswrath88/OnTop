namespace OnTop.Models;

/// <summary>
/// One auto-pin rule: when a visible window matches (process name or title
/// contains Pattern, case-insensitive), OnTop pins it always-on-top and tracks it
/// as an OnTop pin (so the Pinned Windows viewer badges it and Unpin All
/// releases it).
///
/// SECURITY: rules only ever cause a Z-order change (SetWindowPos topmost) on the
/// matched window — never input, never memory access, never process launch. The
/// whole feature is OFF by default (WindowRulesEnabled).
/// </summary>
public class WindowRule : ObservableObject
{
    private bool _enabled = true;
    private string _pattern = "";
    private bool _matchTitle;

    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    /// <summary>Substring to look for (case-insensitive).</summary>
    public string Pattern { get => _pattern; set => Set(ref _pattern, value ?? ""); }

    /// <summary>false = match the process name (default); true = match the window title.</summary>
    public bool MatchTitle { get => _matchTitle; set => Set(ref _matchTitle, value); }

    public WindowRule Clone() => new() { Enabled = Enabled, Pattern = Pattern, MatchTitle = MatchTitle };
}
