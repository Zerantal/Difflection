import assert from 'node:assert/strict';
import test from 'node:test';
import {isSupportedImageFile, setupBrowserDragDrop} from '../wwwroot/dragDropController.mjs';

test('browser drop forwards a single image file', async () => {
    const calls = [];
    const document = createDocument();
    const controller = setupBrowserDragDrop({
        document,
        getBrowserExports: async () => ({
            Difflection: {
                Browser: {
                    BrowserDropBridge: {
                        AcceptDroppedFile: async (name, bytes) => {
                            calls.push({kind: 'single', name, bytes: Array.from(bytes)});
                        },
                        AcceptDroppedPair: async () => {
                            throw new Error('pair should not be called');
                        },
                    },
                },
            },
        }),
        isSupportedImageFile,
    });

    controller.setup();
    document.listeners.dragenter({
        dataTransfer: {files: []}, preventDefault() {
        }
    });

    assert.equal(document.body.classList.values.has('difflection-drop-active'), true);
    assert.equal(document.children.length, 1);

    const overlay = document.children[0];
    await overlay.listeners.drop({
        dataTransfer: {files: [createFile('left.png', 'image/png', [1, 2, 3])]},
        preventDefault() {
        },
    });

    assert.deepEqual(calls, [
        {kind: 'single', name: 'left.png', bytes: [1, 2, 3]},
    ]);
    assert.equal(document.body.classList.values.has('difflection-drop-active'), false);
});

test('browser drop forwards a pair of images', async () => {
    const calls = [];
    const document = createDocument();
    const controller = setupBrowserDragDrop({
        document,
        getBrowserExports: async () => ({
            Difflection: {
                Browser: {
                    BrowserDropBridge: {
                        AcceptDroppedFile: async () => {
                            throw new Error('single should not be called');
                        },
                        AcceptDroppedPair: async (leftName, leftBytes, rightName, rightBytes) => {
                            calls.push({
                                kind: 'pair',
                                leftName,
                                leftBytes: Array.from(leftBytes),
                                rightName,
                                rightBytes: Array.from(rightBytes),
                            });
                        },
                    },
                },
            },
        }),
        isSupportedImageFile,
    });

    controller.setup();
    document.listeners.dragenter({
        dataTransfer: {files: []}, preventDefault() {
        }
    });

    const overlay = document.children[0];
    await overlay.listeners.drop({
        dataTransfer: {
            files: [
                createFile('left.png', 'image/png', [1, 2]),
                createFile('right.jpg', 'image/jpeg', [3, 4]),
            ],
        },
        preventDefault() {
        },
    });

    assert.deepEqual(calls, [
        {
            kind: 'pair',
            leftName: 'left.png',
            leftBytes: [1, 2],
            rightName: 'right.jpg',
            rightBytes: [3, 4],
        },
    ]);
});

test('browser drop ignores non-image files', async () => {
    const calls = [];
    const document = createDocument();
    const controller = setupBrowserDragDrop({
        document,
        getBrowserExports: async () => ({
            Difflection: {
                Browser: {
                    BrowserDropBridge: {
                        AcceptDroppedFile: async () => calls.push('single'),
                        AcceptDroppedPair: async () => calls.push('pair'),
                    },
                },
            },
        }),
        isSupportedImageFile,
    });

    controller.setup();
    document.listeners.dragenter({
        dataTransfer: {files: []}, preventDefault() {
        }
    });

    const overlay = document.children[0];
    await overlay.listeners.drop({
        dataTransfer: {files: [createFile('notes.txt', 'text/plain', [1, 2, 3])]},
        preventDefault() {
        },
    });

    assert.deepEqual(calls, []);
    assert.equal(document.body.classList.values.has('difflection-drop-active'), false);
    assert.equal(document.children.length, 0);
});

test('browser drop clears active state after dragleave cancel', () => {
    const document = createDocument();
    const controller = setupBrowserDragDrop({
        document,
        getBrowserExports: async () => ({
            Difflection: {
                Browser: {
                    BrowserDropBridge: {
                        AcceptDroppedFile: async () => {},
                        AcceptDroppedPair: async () => {},
                    },
                },
            },
        }),
        isSupportedImageFile,
    });

    controller.setup();
    document.listeners.dragenter({
        dataTransfer: { files: [] },
        preventDefault() {},
    });

    assert.equal(document.body.classList.values.has('difflection-drop-active'), true);
    assert.equal(document.children.length, 1);

    document.listeners.dragleave({
        dataTransfer: { files: [] },
        preventDefault() {},
    });

    assert.equal(document.body.classList.values.has('difflection-drop-active'), false);
    assert.equal(document.children.length, 0);
});

test('isSupportedImageFile matches image MIME types and extensions', () => {
    const cases = [
        { name: 'photo.png', type: 'image/png', expected: true },
        { name: 'photo.PNG', type: '', expected: true },
        { name: 'scan.JpEg', type: '', expected: true },
        { name: 'legacy.tif', type: '', expected: true },
        { name: 'scan.tiff', type: '', expected: true },
        { name: 'animation.gif', type: 'image/gif', expected: true },
        { name: 'unknown.bin', type: 'image/webp', expected: true },
        { name: 'document.pdf', type: 'application/pdf', expected: false },
        { name: 'notes.txt', type: 'text/plain', expected: false },
        { name: 'no-extension', type: '', expected: false },
    ];

    for (const { name, type, expected } of cases) {
        assert.equal(
            isSupportedImageFile(createFile(name, type, [])),
            expected,
            `${name} (${type || 'no mime'})`,
        );
    }
});

function createDocument() {
    const listeners = {};
    const bodyClassValues = new Set();
    const document = {
        children: [],
        listeners,
        body: {
            classList: {
                values: bodyClassValues,
                toggle(value, enabled) {
                    if (enabled) {
                        bodyClassValues.add(value);
                    } else {
                        bodyClassValues.delete(value);
                    }
                },
            },
            appendChild(child) {
                document.children.push(child);
                return child;
            },
        },
        createElement() {
            const elementListeners = {};
            return {
                listeners: elementListeners,
                style: {},
                addEventListener(name, handler) {
                    elementListeners[name] = handler;
                },
                remove() {
                    const index = document.children.indexOf(this);
                    if (index >= 0) {
                        document.children.splice(index, 1);
                    }
                },
            };
        },
        addEventListener(name, handler) {
            listeners[name] = handler;
        },
    };

    return document;
}

function createFile(name, type, bytes) {
    return {
        name,
        type,
        async arrayBuffer() {
            return new Uint8Array(bytes).buffer;
        },
    };
}
