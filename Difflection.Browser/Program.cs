using System;
using Avalonia;
using Avalonia.Browser;
using Difflection;
using System.Threading.Tasks;

namespace Difflection.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        return BuildAvaloniaApp()
            .StartBrowserAppAsync("out", new BrowserPlatformOptions 
            { 
                RenderingMode =
                [
                    BrowserRenderingMode.WebGL2, 
                    BrowserRenderingMode.WebGL1, 
                    BrowserRenderingMode.Software2D
                ]
            });
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .WithInterFont()
            .UseBrowser()
            .LogToTrace();
    
    
}


