import { dotnet } from './_framework/dotnet.js';
import { isSupportedImageFile, setupBrowserDragDrop } from './dragDropController.mjs';
import { showBrowserError, showDropBridgeError } from './browserShell.mjs';

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

let difflectionExportsPromise = null;

async function getBrowserExports() {
    difflectionExportsPromise ??= (async () => {
        const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
        return await getAssemblyExports("Difflection.Browser.dll");
    })();

    return difflectionExportsPromise;
}

try {
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    const dragDrop = setupBrowserDragDrop({
        document,
        getBrowserExports,
        isSupportedImageFile,
        showDropBridgeError,
    });

    dragDrop.setup();

    const config = dotnetRuntime.getConfig();

    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (error) {
    const message = error instanceof Error
        ? error.message
        : "The browser host failed to initialize. Reload the page and try again.";
    showBrowserError(document, message);
    throw error;
}
