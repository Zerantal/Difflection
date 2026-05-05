import { dotnet } from './_framework/dotnet.js';
import { isSupportedImageFile, setupBrowserDragDrop } from './dragDropController.mjs';

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

let difflectionExportsPromise = null;

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const dragDrop = setupBrowserDragDrop({
    document,
    getBrowserExports,
    isSupportedImageFile,
});

dragDrop.setup();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);

async function getBrowserExports() {
    difflectionExportsPromise ??= (async () => {
        const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
        return await getAssemblyExports("Difflection.Browser.dll");
    })();

    return difflectionExportsPromise;
}
