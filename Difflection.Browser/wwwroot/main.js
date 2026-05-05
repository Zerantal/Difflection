import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

let difflectionExportsPromise = null;
let dragDepth = 0;
let browserDropHandlersInstalled = false;
let dropOverlay = null;

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

    const setDropActive = (value) => {
        document.body.classList.toggle("difflection-drop-active", value);
    };

    const shouldHandle = (event) => event.dataTransfer != null;

    const ensureDropOverlay = () => {
        if (dropOverlay != null) {
            return dropOverlay;
        }

        dropOverlay = document.createElement("div");
        dropOverlay.id = "difflection-drop-overlay";
        dropOverlay.style.position = "fixed";
        dropOverlay.style.inset = "0";
        dropOverlay.style.zIndex = "2147483647";
        dropOverlay.style.background = "transparent";
        dropOverlay.style.pointerEvents = "auto";

        dropOverlay.addEventListener("dragover", (event) => {
            if (!shouldHandle(event)) {
                return;
            }

            event.preventDefault();
            event.dataTransfer.dropEffect = "copy";
        });

        dropOverlay.addEventListener("drop", async (event) => {
            if (!shouldHandle(event)) {
                return;
            }

            event.preventDefault();
            dragDepth = 0;
            setDropActive(false);
            dropOverlay?.remove();
            dropOverlay = null;

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
        });

        document.body.appendChild(dropOverlay);
        return dropOverlay;
    };

    const releaseDropOverlay = () => {
        dropOverlay?.remove();
        dropOverlay = null;
    };

    document.addEventListener("dragenter", (event) => {
        if (!shouldHandle(event)) {
            return;
        }

        dragDepth += 1;
        ensureDropOverlay();
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
            releaseDropOverlay();
        }
    }, true);

    document.addEventListener("drop", (event) => {
        if (!shouldHandle(event)) {
            return;
        }

        event.preventDefault();
        dragDepth = 0;
        setDropActive(false);
        releaseDropOverlay();
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
