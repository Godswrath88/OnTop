using OnTop.Helpers;
using OnTop.Services;
using Xunit;

namespace OnTop.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+N", Win32.MOD_CONTROL | Win32.MOD_ALT, 0x4E)]
    [InlineData("ctrl + alt + q", Win32.MOD_CONTROL | Win32.MOD_ALT, 0x51)]
    [InlineData("F10", 0u, 0x79)]
    [InlineData("Shift+Win+F1", Win32.MOD_SHIFT | Win32.MOD_WIN, 0x70)]
    public void ParseHotkey_reads_valid_combos(string combo, uint mods, uint vk)
    {
        Assert.True(HotkeyService.ParseHotkey(combo, out var m, out var v));
        Assert.Equal(mods, m);
        Assert.Equal(vk, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+NotAKey")]
    public void ParseHotkey_rejects_invalid_combos(string combo)
    {
        Assert.False(HotkeyService.ParseHotkey(combo, out _, out _));
    }

    [Fact]
    public void Register_returns_minus3_for_an_invalid_combo()
    {
        using var hk = new HotkeyService();
        Assert.Equal(-3, hk.Register("Ctrl+NotAKey", () => { }));
    }

    [Fact]
    public void A_released_id_is_not_handed_straight_to_the_next_hotkey()
    {
        // Regression (0.45.3 review): lowest-free id reuse let a stale id or a
        // queued WM_HOTKEY reach a different hotkey. Thread-level registration
        // (no window); an obscure combo so it can't clash with a real one.
        using var hk = new HotkeyService();
        int first = hk.Register("Ctrl+Alt+Shift+F24", () => { });
        if (first < 0) return;   // combo held elsewhere on this machine: nothing to check
        hk.Unregister(first);
        int second = hk.Register("Ctrl+Alt+Shift+F24", () => { });
        try
        {
            Assert.True(second >= 0, "re-register failed");
            Assert.NotEqual(first, second);
        }
        finally { hk.Unregister(second); }
    }
}
