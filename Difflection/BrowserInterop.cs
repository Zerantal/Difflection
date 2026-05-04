using System;
using Difflection.Views;

namespace Difflection;

public static class BrowserInterop
{
    public static Action<MainView>? AttachBrowserBridge { get; set; }

    public static Action<MainView>? DetachBrowserBridge { get; set; }
}
