using System;

namespace OnTop.Models;

/// <summary>
/// One piece of POWERFUL content that arrived from OUTSIDE the app (an explicit
/// import or a restore from a non-OnTop path) and was neutralized on the way in
/// (SECURITY.md "Shared / imported configs — validate content, don't trust
/// authority"). It lands INERT; the user sees it VERBATIM in the Import Review
/// window and may opt it back in per item. <see cref="Restore"/> re-applies the
/// original value — it is invoked ONLY from that review, never automatically.
///
/// The app's OWN settings + backups never produce these (provenance rule):
/// recovery from an own backup loads fully active and is never re-approved.
/// </summary>
public class QuarantineItem
{
    /// <summary>Short category shown as a badge ("Zone Action", "Updater URL", "Macro").</summary>
    public string Category { get; init; } = "";
    /// <summary>What it belongs to (zone name, macro name, ...).</summary>
    public string Subject { get; init; } = "";
    /// <summary>The powerful content, shown VERBATIM (command line, process name, URL, hotkey).</summary>
    public string Detail { get; init; } = "";
    /// <summary>Re-enables the original value. Called only when the user ticks
    /// the item in the review window.</summary>
    public Action Restore { get; init; } = () => { };
    /// <summary>User decision from the review window (default: stays inert).</summary>
    public bool Keep { get; set; }
}
