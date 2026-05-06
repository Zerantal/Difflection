using Avalonia;
using Avalonia.Headless;
using Difflection.Tests.Infrastructure;

[assembly: AvaloniaTestApplication(typeof(AvaloniaTestApp))]

namespace Difflection.Tests.Infrastructure;

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
