namespace OnTop.Models;

public enum ConfigConflictKind
{
    /// <summary>Settings file was written by a newer OnTop; loaded best-effort.</summary>
    NewerVersion,
    /// <summary>Settings file(s) were unreadable/corrupt; fell back to defaults.</summary>
    Corrupt,
}

/// <summary>
/// Describes a settings-load situation the user should be told about in plain
/// language (see <c>ConfigConflictDialog</c>). Not an error to swallow — the user
/// gets Continue (keep going) or Cancel (close so they can restore a backup).
/// </summary>
public sealed record ConfigConflict(
    ConfigConflictKind Kind,
    int FileSchema,
    int AppSchema,
    string? SourceFile)
{
    /// <summary>A friendly, non-technical explanation for the dialog body.</summary>
    public string FriendlyMessage => Kind switch
    {
        ConfigConflictKind.NewerVersion =>
            "Your saved OnTop settings were created by a newer version of the app. " +
            "OnTop loaded everything it recognizes, but a few newer options may be " +
            "missing until you update.\n\n" +
            "You can keep going safely. Your original settings file has NOT been changed " +
            "— if you'd rather not risk it, choose Cancel and update OnTop (or restore a " +
            "backup) first.",
        _ =>
            "OnTop couldn't read your saved settings — the file may be damaged. " +
            "It has started with default settings so you can keep using it.\n\n" +
            "Your zones and preferences may be missing. If you have a backup you'd like " +
            "to restore, choose Cancel to close OnTop first. Choose Continue to start " +
            "fresh with defaults.",
    };

    public string FriendlyTitle => Kind == ConfigConflictKind.NewerVersion
        ? "Settings From a Newer Version"
        : "Couldn't Read Your Settings";
}
