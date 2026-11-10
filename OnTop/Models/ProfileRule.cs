namespace OnTop.Models;

/// <summary>S28 Stage G — "while process X runs, use profile Y". Detected by
/// the same 5 s watcher tick as auto-stealth (names only). Off by default.</summary>
public class ProfileRule : ObservableObject
{
    private bool _enabled = true;
    private string _process = "";
    private string _profile = "";
    private bool _revertWhenGone = true;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    /// <summary>Process name without .exe.</summary>
    public string Process { get => _process; set => Set(ref _process, value ?? ""); }
    public string Profile { get => _profile; set => Set(ref _profile, value ?? ""); }
    /// <summary>Switch back to the previous profile when the process exits.</summary>
    public bool RevertWhenGone { get => _revertWhenGone; set => Set(ref _revertWhenGone, value); }
    public ProfileRule Clone() => new() { Enabled = Enabled, Process = Process, Profile = Profile, RevertWhenGone = RevertWhenGone };
}
