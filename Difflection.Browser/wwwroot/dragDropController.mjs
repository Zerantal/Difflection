export function setupBrowserDragDrop({ document, getBrowserExports, isSupportedImageFile, showDropBridgeError }) {
    let dragDepth = 0;
    let dropOverlay = null;
    let browserDropHandlersInstalled = false;

    const setup = () => {
        if (browserDropHandlersInstalled) {
            return;
        }

        browserDropHandlersInstalled = true;

        const setDropActive = (value) => {
            document.body.classList.toggle("difflection-drop-active", value);
        };

        const shouldHandle = (event) => event.dataTransfer != null;

        const reportDropError = (message) => {
            dragDepth = 0;
            setDropActive(false);
            dropOverlay?.remove();
            dropOverlay = null;
            showDropBridgeError?.(document, message);
        };

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
                    reportDropError("No supported image files found. Use PNG, JPEG, BMP, GIF, WebP, or TIFF.");
                    return;
                }

                try {
                    const exports = await getBrowserExports();
                    const bridge = exports?.Difflection?.Browser?.BrowserDropBridge;
                    if (bridge == null) {
                        reportDropError("Difflection is still loading. Wait a moment and try dropping again.");
                        return;
                    }

                    if (files.length >= 2) {
                        await bridge.AcceptDroppedPair(
                            files[0].name,
                            new Uint8Array(await files[0].arrayBuffer()),
                            files[1].name,
                            new Uint8Array(await files[1].arrayBuffer()));
                        return;
                    }

                    await bridge.AcceptDroppedFile(
                        files[0].name,
                        new Uint8Array(await files[0].arrayBuffer()));
                } catch (error) {
                    const message = error instanceof Error
                        ? `Could not load dropped images: ${error.message}`
                        : "Could not load dropped images. Try again.";
                    reportDropError(message);
                }
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
    };

    const reset = () => {
        dragDepth = 0;
        dropOverlay?.remove();
        dropOverlay = null;
        document.body.classList.toggle("difflection-drop-active", false);
    };

    return { setup, reset };
}

export function isSupportedImageFile(file) {
    const name = (file.name ?? "").toLowerCase();
    return file.type.startsWith("image/")
        || /\.(png|jpe?g|bmp|gif|webp|tiff?)$/i.test(name);
}
