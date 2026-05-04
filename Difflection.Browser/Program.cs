using Avalonia;
using Avalonia.Browser;
using Difflection;
using System.Threading.Tasks;

namespace Difflection.Browser;

internal sealed partial class Program
{
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
