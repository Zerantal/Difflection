using System;
using System.Reflection;
using Difflection.Views;
using Xunit;

namespace Difflection.Tests.Views;

public sealed class PixelRulerTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(10.0)]
    [InlineData(25.0)]
    [InlineData(100.0)]
    public void Tick_step_is_never_smaller_than_one_image_pixel(double zoomScale)
    {
        var method = typeof(PixelRuler).GetMethod("ChooseTickStep", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(PixelRuler).FullName, "ChooseTickStep");

        var step = (double)method.Invoke(null, [zoomScale])!;

        Assert.True(step >= 1.0, $"Expected tick step >= 1.0 for zoom {zoomScale}, got {step}.");
    }
}
