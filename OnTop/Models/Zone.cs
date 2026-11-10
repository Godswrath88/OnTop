using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace OnTop.Models;

/// <summary>What a zone does when its double-click registers.</summary>
public enum ZoneAction
{
    /// <summary>Exit OnTop (the classic behavior).</summary>
    Exit = 0,
    /// <summary>Enter Stealth Mode instead: unhook the mouse so anti-cheats
    /// see nothing, but keep the app alive in the tray.</summary>
    Stealth = 1,
    /// <summary>Run the zone's ActionCommand via the shell (user-authored;
    /// logged and toasted on every run — see SECURITY.md "Per-Zone Actions").</summary>
    RunCommand = 2,
    /// <summary>Kill every process matching ActionProcess (system-critical
    /// names are refused; logged with the terminated count).</summary>
    KillProcess = 3,
    /// <summary>Toggle ActionProcess's main window: minimized → restore +
    /// foreground; otherwise minimize.</summary>
    ToggleWindow = 4,
    /// <summary>Play the macro identified by ActionMacroId (macro module must
    /// be enabled; all playback guards apply).</summary>
    RunMacro = 5,
    /// <summary>S28 Stage E: apply the placement rule identified by
    /// ActionPlacementId (placement feature must be on; launch stays gated).</summary>
    ApplyPlacement = 6,
    /// <summary>S28 Stage F: run the library action identified by ActionLibraryId.</summary>
    Library = 7
}

/// <summary>How the zone was recorded and how it detects clicks.</summary>
public enum ZoneKind
{
    /// <summary>Classic center point — hit area from Zone Size / Tolerance / Variation.</summary>
    Point = 0,
    /// <summary>Recorded by dragging a rectangle — sized 1:1 (Tolerance and Variation start at 0).</summary>
    Box = 1,
    /// <summary>Freeform polygon — detection is ray-cast point-in-polygon; the sliders do not apply.</summary>
    Draw = 2
}

/// <summary>A polygon vertex. For Zone.RelPoints these are offsets from the
/// zone's anchor (X, Y), which is why moving the anchor moves the whole shape.</summary>
public class ZonePoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public ZonePoint() { }
    public ZonePoint(int x, int y) { X = x; Y = y; }
}

public class Zone : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N")[..8];
    private string _name = "New Zone";
    private int _x, _y;
    private int _tolerance = 5;
    private int _capX = 20, _capY = 20;
    private int _variation = 5;
    private bool _enabled = true;
    private bool _showDot;
    private string _group = "Default";
    private int _monitor;
    private int _colorIndex;
    private ZoneAction _triggerAction;
    private ZoneKind _kind;

    public string Id { get => _id; set => Set(ref _id, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public int X { get => _x; set => Set(ref _x, value); }
    public int Y { get => _y; set => Set(ref _y, value); }
    public int Tolerance { get => _tolerance; set => Set(ref _tolerance, Math.Max(0, value)); }
    public int CapX { get => _capX; set => Set(ref _capX, Math.Max(0, value)); }
    public int CapY { get => _capY; set => Set(ref _capY, Math.Max(0, value)); }
    public int Variation { get => _variation; set => Set(ref _variation, Math.Max(0, value)); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public bool ShowDot { get => _showDot; set => Set(ref _showDot, value); }
    public string Group { get => _group; set => Set(ref _group, value ?? "Default"); }
    public int Monitor { get => _monitor; set { if (Set(ref _monitor, value)) OnPropertyChanged(nameof(MonitorLabel)); } }
    /// <summary>1-based monitor number for display (matches the "Monitor N" menus).</summary>
    [JsonIgnore] public int MonitorLabel => _monitor + 1;
    public int ColorIndex { get => _colorIndex; set => Set(ref _colorIndex, Math.Max(0, value)); }
    /// <summary>Exit (default) or Stealth — what this zone's double-click does.
    /// AppSettings.ZonesTriggerStealth overrides all zones to Stealth.</summary>
    public ZoneAction TriggerAction { get => _triggerAction; set => Set(ref _triggerAction, value); }

    private string _actionCommand = "";
    /// <summary>Command line for RunCommand (shown verbatim in the UI).</summary>
    public string ActionCommand { get => _actionCommand; set => Set(ref _actionCommand, value ?? ""); }

    private string _actionProcess = "";
    /// <summary>Process name (no .exe needed) for KillProcess / ToggleWindow.</summary>
    public string ActionProcess { get => _actionProcess; set => Set(ref _actionProcess, value ?? ""); }

    private string _actionMacroId = "";
    /// <summary>Macro Id for the RunMacro action.</summary>
    public string ActionMacroId { get => _actionMacroId; set => Set(ref _actionMacroId, value ?? ""); }
    private string _actionPlacementId = "";
    /// <summary>S28 Stage E: PlacementRule.Id for ZoneAction.ApplyPlacement.</summary>
    public string ActionPlacementId { get => _actionPlacementId; set => Set(ref _actionPlacementId, value ?? ""); }
    private string _actionLibraryId = "";
    private int _gesture;
    /// <summary>S28 Stage G — what triggers this zone: 0 double left-click
    /// (classic), 1 middle click, 2 triple left-click, 3 Ctrl + double left,
    /// 4 double right-click. The hook already sees all of them.</summary>
    public int Gesture { get => _gesture; set => Set(ref _gesture, value); }
    public static readonly string[] GestureNames = { "Double Left-Click", "Middle Click", "Triple Left-Click", "Ctrl + Double Left-Click", "Double Right-Click", "Shift + Double Left-Click", "Alt + Double Left-Click" };
    private int _cooldownSec, _schedStart = -1, _schedEnd = -1, _schedDays = 0x7F;
    /// <summary>S28 Stage I: ignore re-triggers for this many seconds after a hit (0 = none).</summary>
    public int CooldownSec { get => _cooldownSec; set => Set(ref _cooldownSec, value); }
    /// <summary>S28 Stage I: optional active window (minutes since midnight; -1 = always) and day mask (bit 0 = Mon).</summary>
    public int SchedStart { get => _schedStart; set => Set(ref _schedStart, value); }
    public int SchedEnd { get => _schedEnd; set => Set(ref _schedEnd, value); }
    public int SchedDays { get => _schedDays; set => Set(ref _schedDays, value); }
    public bool IsScheduledNow(System.DateTime now)
    {
        int dow = ((int)now.DayOfWeek + 6) % 7;
        if ((SchedDays & (1 << dow)) == 0) return false;
        if (SchedStart < 0 || SchedEnd < 0) return true;
        int cur = now.Hour * 60 + now.Minute;
        return SchedStart <= SchedEnd ? cur >= SchedStart && cur < SchedEnd : cur >= SchedStart || cur < SchedEnd;
    }
    /// <summary>S28 Stage F: LibraryAction.Id for ZoneAction.Library.</summary>
    public string ActionLibraryId { get => _actionLibraryId; set => Set(ref _actionLibraryId, value ?? ""); }
    /// <summary>Point (default), Box, or Draw — set at record time.</summary>
    public ZoneKind Kind { get => _kind; set => Set(ref _kind, value); }

    /// <summary>Draw zones only: polygon vertices relative to the anchor (X, Y).
    /// Stored relative so moving the anchor moves the whole shape, and so JSON
    /// property order can never desync the polygon from its position.</summary>
    public List<ZonePoint> RelPoints { get; set; } = new();

    [JsonIgnore] private bool IsPolygon => _kind == ZoneKind.Draw && RelPoints.Count >= 3;

    /// <summary>Half-extent of the hit area. Point/Box: the v9.5 formula
    /// half = (Size/2) + Tolerance + Variation. Draw: the polygon's bounding
    /// half-extent (used for handles, labels, and the map — not detection).</summary>
    [JsonIgnore] public int HalfX => IsPolygon
        ? Math.Max(1, RelPoints.Max(p => Math.Abs(p.X)))
        : (CapX / 2) + Tolerance + Variation;

    [JsonIgnore] public int HalfY => IsPolygon
        ? Math.Max(1, RelPoints.Max(p => Math.Abs(p.Y)))
        : (CapY / 2) + Tolerance + Variation;

    private string _healthWarning = "";
    /// <summary>Non-empty when the VM has flagged this zone (off-screen, no hit
    /// area, or a duplicate name) — the list shows a ⚠ with this as the tooltip.
    /// Recomputed by MainViewModel; not persisted.</summary>
    [JsonIgnore] public string HealthWarning { get => _healthWarning; set => Set(ref _healthWarning, value); }

    /// <summary>Does a physical-pixel click land in this zone? Point/Box use the
    /// classic box test; Draw zones ray-cast against the real polygon outline.</summary>
    public bool Contains(int px, int py)
    {
        if (IsPolygon) return PointInPolygon(px - _x, py - _y);
        return Math.Abs(px - _x) <= HalfX && Math.Abs(py - _y) <= HalfY;
    }

    /// <summary>Even-odd ray cast in anchor-relative coordinates (v9.5 Draw detection).</summary>
    private bool PointInPolygon(int rx, int ry)
    {
        bool inside = false;
        var pts = RelPoints;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            var a = pts[i];
            var b = pts[j];
            if ((a.Y > ry) != (b.Y > ry) &&
                rx < (double)(b.X - a.X) * (ry - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    // ── Draw-zone scaling (bounding-box handles, drag-start snapshot) ────
    [JsonIgnore] private List<ZonePoint>? _scaleSnapshot;

    /// <summary>A handle drag is starting on a Draw zone — snapshot the polygon
    /// so the whole drag scales from the original vertices (repeated integer
    /// scaling of already-scaled points would erode the shape).</summary>
    public void BeginScale()
    {
        if (_kind == ZoneKind.Draw)
            _scaleSnapshot = RelPoints.Select(p => new ZonePoint(p.X, p.Y)).ToList();
    }

    /// <summary>The handle drag ended — drop the scale snapshot.</summary>
    public void EndScale() => _scaleSnapshot = null;

    /// <summary>Resize the hit area toward the desired half-extents.
    /// Point/Box: the waterfall rule — shrink CapX/CapY first; once the binding
    /// (smaller) axis hits zero cap, eat into Tolerance, then Variation.
    /// Draw: scale the polygon from the drag-start snapshot to fit the new
    /// bounding half-extents.</summary>
    public void ResizeTo(int desiredHalfX, int desiredHalfY)
    {
        desiredHalfX = Math.Max(0, desiredHalfX);
        desiredHalfY = Math.Max(0, desiredHalfY);

        if (IsPolygon)
        {
            if (_scaleSnapshot is not { Count: >= 3 })
                _scaleSnapshot = RelPoints.Select(p => new ZonePoint(p.X, p.Y)).ToList();
            List<ZonePoint> source = _scaleSnapshot;
            double srcHalfX = Math.Max(1, source.Max(p => Math.Abs(p.X)));
            double srcHalfY = Math.Max(1, source.Max(p => Math.Abs(p.Y)));
            double fx = Math.Max(2, desiredHalfX) / srcHalfX;
            double fy = Math.Max(2, desiredHalfY) / srcHalfY;
            int count = Math.Min(RelPoints.Count, source.Count);
            for (int i = 0; i < count; i++)
            {
                RelPoints[i].X = (int)Math.Round(source[i].X * fx);
                RelPoints[i].Y = (int)Math.Round(source[i].Y * fy);
            }
            NotifyShapeChanged();
            return;
        }

        int tol = Tolerance, vr = Variation;
        int binding = Math.Min(desiredHalfX, desiredHalfY);
        if (binding < tol + vr)
        {
            if (binding >= vr) tol = binding - vr;
            else { tol = 0; vr = binding; }
        }

        Tolerance = tol;
        Variation = vr;
        CapX = Math.Max(0, (desiredHalfX - tol - vr) * 2);
        CapY = Math.Max(0, (desiredHalfY - tol - vr) * 2);
    }

    /// <summary>Turn this zone into a Draw zone from absolute physical-pixel
    /// vertices: the anchor becomes the bounding-box center and every vertex is
    /// stored relative to it.</summary>
    public void SetPolygonFromPhysical(IReadOnlyList<ZonePoint> physical)
    {
        if (physical.Count < 3) return;
        int minX = physical.Min(p => p.X), maxX = physical.Max(p => p.X);
        int minY = physical.Min(p => p.Y), maxY = physical.Max(p => p.Y);
        int cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        Kind = ZoneKind.Draw;
        X = cx;
        Y = cy;
        RelPoints = physical.Select(p => new ZonePoint(p.X - cx, p.Y - cy)).ToList();
        NotifyShapeChanged();
    }

    /// <summary>Absolute polygon vertices (anchor + offsets) for rendering.</summary>
    public IEnumerable<ZonePoint> GetAbsolutePoints()
        => RelPoints.Select(p => new ZonePoint(_x + p.X, _y + p.Y));

    private void NotifyShapeChanged()
    {
        OnPropertyChanged(nameof(RelPoints));
        OnPropertyChanged(nameof(HalfX));
        OnPropertyChanged(nameof(HalfY));
    }

    public Zone Clone() => new()
    {
        Id = Id, Name = Name, X = X, Y = Y,
        Tolerance = Tolerance, CapX = CapX, CapY = CapY, Variation = Variation,
        Enabled = Enabled, ShowDot = ShowDot, Group = Group, Monitor = Monitor,
        ColorIndex = ColorIndex, TriggerAction = TriggerAction, Kind = Kind,
        // S28 BUG FIX: action payloads were NOT cloned — profile snapshots and
        // Duplicate silently lost Run Command / Kill / Macro settings.
        ActionCommand = ActionCommand, ActionProcess = ActionProcess, ActionMacroId = ActionMacroId,
        ActionPlacementId = ActionPlacementId, ActionLibraryId = ActionLibraryId, Gesture = Gesture,
        CooldownSec = CooldownSec, SchedStart = SchedStart, SchedEnd = SchedEnd, SchedDays = SchedDays,
        RelPoints = RelPoints.Select(p => new ZonePoint(p.X, p.Y)).ToList()
    };
}
