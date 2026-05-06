using Avalonia;
using Avalonia.Browser;
using System.Threading.Tasks;

namespace Difflection.Browser;

internal static class Program
{
    // ReSharper disable once UnusedParameter.Local
    private static Task Main(string[] args)
    {
        BrowserInterop.AttachBrowserBridge = BrowserDropBridge.Attach;
        BrowserInterop.DetachBrowserBridge = BrowserDropBridge.Detach;

        return BuildAvaloniaApp()
            .StartBrowserAppAsync("out");
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .WithInterFont()
            .UseBrowser()
            .LogToTrace();
}
