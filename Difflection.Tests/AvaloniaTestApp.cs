using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;
using Difflection;

[assembly: AvaloniaTestApplication(typeof(Difflection.Tests.AvaloniaTestApp))]

namespace Difflection.Tests;

public static class AvaloniaTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            })
            .LogToTrace();
}
