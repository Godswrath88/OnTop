using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OnTop.Models;

/// <summary>
/// One recorded input macro (see SECURITY.md "Macro Module"). Events use
/// compact one-letter JSON names — full-fidelity recordings hold thousands.
/// Coordinates are stored as PERMILLE-OF-65535 fractions of the virtual
/// desktop at record time, so playback adapts to the layout at play time
/// (roadmap: resolution/layout awareness).
/// </summary>
public class Macro
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Macro";
    public bool Enabled { get; set; } = true;
    /// <summary>Optional global hotkey. Press to play; press again to stop.
    /// Must pass the shared conflict check (one combo, one feature).</summary>
    public string Hotkey { get; set; } = "";
    /// <summary>Playback speed multiplier (0.1–10; 1 = recorded timing).</summary>
    public double Speed { get; set; } = 1.0;
    /// <summary>Repeat count; 0 = loop until stopped (1–1000 otherwise).</summary>
    public int Repeat { get; set; } = 1;
    /// <summary>Humanizing jitter in PIXELS (0–20): every mouse event lands
    /// within ±N px of its recorded spot. 0 = exact replay (default).</summary>
    public int Jitter { get; set; }
    public List<MacroEvent> Events { get; set; } = new();
    /// <summary>S28: DPAPI-protected Events (base64), present ONLY on disk when
    /// AppSettings.EncryptMacros is on. In memory Events is always the live,
    /// decrypted list and this is null. See SettingsService.SealMacros/UnsealMacros.</summary>
    public string? EncryptedEvents { get; set; }
    /// <summary>S28 Stage G — chaining: when this macro finishes naturally (not
    /// stopped), play this one next (depth-capped at 5; no self-loops).</summary>
    public string NextMacroId { get; set; } = "";
    /// <summary>S28 Stage G — relative replay: title (contains) and top-left of
    /// the window that was in front when recording started. With Relative on,
    /// playback finds that window and shifts every mouse position by however
    /// far it has moved since.</summary>
    public string AnchorTitle { get; set; } = "";
    public int AnchorLeft { get; set; }
    public int AnchorTop { get; set; }
    public bool Relative { get; set; }
    /// <summary>S28 Stage H — repeat-until-key: while looping (Repeat = 0), stop
    /// as soon as this virtual-key is pressed (0 = none; polled, no hook).</summary>
    public int StopKey { get; set; }

    [JsonIgnore]
    public int DurationMs => Events.Count == 0 ? 0 : (int)Events[^1].T;
}

/// <summary>Kinds: 0 Move · 1/2 LDown/LUp · 3/4 RDown/RUp · 5/6 MDown/MUp ·
/// 7 Wheel (D = signed delta) · 8/9 KeyDown/KeyUp (D = virtual-key code) ·
/// 10 WAIT FOR WINDOW (S = title contains, D = timeout ms) — a wait, not a verb.</summary>
public class MacroEvent
{
    /// <summary>Milliseconds since recording start.</summary>
    public uint T { get; set; }
    public byte K { get; set; }
    /// <summary>Normalized X/Y in 0..65535 across the virtual desktop.</summary>
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>Wheel delta or vkCode, by kind.</summary>
    public int D { get; set; }
    /// <summary>Kind 10 only: window-title substring to wait for.</summary>
    public string? S { get; set; }
    /// <summary>S28 Stage I: optional user note for this step (editor "Note" column).</summary>
    public string? L { get; set; }
}
