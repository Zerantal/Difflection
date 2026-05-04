import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

let difflectionExportsPromise = null;
let dragDepth = 0;
let browserDropHandlersInstalled = false;

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

setupBrowserDragDrop();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);

function setupBrowserDragDrop() {
    if (browserDropHandlersInstalled) {
        return;
    }

    browserDropHandlersInstalled = true;

    const hasFileDrop = (event) => event.dataTransfer != null && Array.from(event.dataTransfer.types ?? []).includes("Files");
    const setDropActive = (value) => {
        document.body.classList.toggle("difflection-drop-active", value);
    };

    const shouldHandle = (event) => event.dataTransfer != null;

    document.addEventListener("dragenter", (event) => {
        if (!shouldHandle(event)) {
            return;
        }

        dragDepth += 1;
        setDropActive(true);
        event.preventDefault();
    }, true);

    document.addEventListener("dragover", (event) => {
        if (!shouldHandle(event)) {
            return;
        }

        event.preventDefault();
        event.dataTransfer.dropEffect = "copy";
    }, true);

    document.addEventListener("dragleave", (event) => {
        if (!shouldHandle(event)) {
            return;
        }

        dragDepth = Math.max(0, dragDepth - 1);
        if (dragDepth === 0) {
            setDropActive(false);
        }
    }, true);

    document.addEventListener("drop", async (event) => {
        if (!shouldHandle(event)) {
            return;
        }

        event.preventDefault();
        dragDepth = 0;
        setDropActive(false);

        const files = Array.from(event.dataTransfer.files ?? []).filter(isSupportedImageFile).slice(0, 2);
        if (files.length === 0) {
            return;
        }

        const exports = await getBrowserExports();

        if (files.length >= 2) {
            await exports.Difflection.BrowserDropBridge.AcceptDroppedPair(
                files[0].name,
                new Uint8Array(await files[0].arrayBuffer()),
                files[1].name,
                new Uint8Array(await files[1].arrayBuffer()));
            return;
        }

        await exports.Difflection.BrowserDropBridge.AcceptDroppedFile(
            files[0].name,
            new Uint8Array(await files[0].arrayBuffer()));
    }, true);
}

async function getBrowserExports() {
    difflectionExportsPromise ??= (async () => {
        const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
        return await getAssemblyExports("Difflection.Browser.dll");
    })();

    return difflectionExportsPromise;
}

function isSupportedImageFile(file) {
    const name = (file.name ?? "").toLowerCase();
    return file.type.startsWith("image/")
        || /\.(png|jpe?g|bmp|gif|webp|tiff?)$/i.test(name);
}
