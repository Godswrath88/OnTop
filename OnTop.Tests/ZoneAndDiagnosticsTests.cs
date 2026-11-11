using System;
using OnTop.Models;
using OnTop.Services;
using Xunit;

namespace OnTop.Tests;

public class ZoneTests
{
    [Fact]
    public void Box_zone_hit_test_uses_half_size_plus_tolerance_plus_variation()
    {
        // Detection math (must not change): half = Size/2 + Tolerance + Variation.
        var z = new Zone { X = 100, Y = 200, CapX = 20, CapY = 10, Tolerance = 2, Variation = 1 };
        Assert.Equal(13, z.HalfX);
        Assert.Equal(8, z.HalfY);

        Assert.True(z.Contains(100, 200));
        Assert.True(z.Contains(113, 200));    // right edge, inclusive
        Assert.False(z.Contains(114, 200));
        Assert.True(z.Contains(100, 192));    // top edge, inclusive
        Assert.False(z.Contains(100, 191));
    }
}

public class DiagnosticsTests
{
    [Theory]
    [InlineData(@"C:\somewhere\settings.json")]
    [InlineData(@"C:\somewhere\bundle")]
    public void WriteBundle_refuses_anything_but_a_zip_before_touching_disk(string path)
    {
        // A mistyped Export name must never truncate a real file.
        Assert.Throws<ArgumentException>(() => DiagnosticsService.WriteBundle(null!, path));
    }
}
