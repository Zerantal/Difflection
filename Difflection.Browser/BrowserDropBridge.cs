using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Difflection.Views;

namespace Difflection;

[SupportedOSPlatform("browser")]
public static partial class BrowserDropBridge
{
    private static MainView? _view;

    internal static void Attach(MainView view)
    {
        _view = view;
    }

    internal static void Detach(MainView view)
    {
        if (ReferenceEquals(_view, view)) 
        {
            _view = null;
        }
    }

    [JSExport]
    public static async Task AcceptDroppedFile(string fileName, byte[] fileBytes)
    {
        var view = _view;
        if (view is null)
        {
            return;
        }

        await view.LoadBrowserDroppedFilesAsync([fileName], [fileBytes]);
    }

    [JSExport]
    public static async Task AcceptDroppedPair(string leftFileName, byte[] leftFileBytes, string rightFileName, byte[] rightFileBytes)
    {
        var view = _view;
        if (view is null)
        {
            return;
        }

        await view.LoadBrowserDroppedFilesAsync(
            [leftFileName, rightFileName],
            [leftFileBytes, rightFileBytes]);
    }
}
