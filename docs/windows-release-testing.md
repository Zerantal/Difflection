# Windows release testing checklist

Use this checklist when validating a Windows build before calling a release ready. It focuses on the desktop app (`Difflection.Desktop`); the browser host is out of scope here.

## Prerequisites

- Windows 10 or 11, x64
- A tagged release asset `difflection-win-x64.zip` from [GitHub Releases](https://github.com/Zerantal/Difflection/releases), or a local publish:

```powershell
dotnet publish Difflection.Desktop/Difflection.Desktop.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output ./artifacts/win-x64
```

- Two or more PNG/JPEG test images (different sizes help exercise zoom and diff rendering)

## Setup notes

- Extract the zip to a writable folder. There is no installer yet.
- Windows SmartScreen may warn on unsigned prerelease binaries — unblock only for local validation.
- The release workflow builds on Ubuntu; smoke-test the published zip, not only a dev `dotnet run` build.

## Verification checklist

Mark each item pass/fail when validating a candidate build.

### Launch and shell

- [ ] App starts without crashing from the extracted folder (double-click or `Difflection.Desktop.exe`)
- [ ] Main window renders toolbar, comparison stage, and empty-state hints
- [ ] Light theme renders correctly; if OS dark mode is enabled, dark theme renders correctly

### Open files

- [ ] `Ctrl + O` opens the file picker
- [ ] Selecting a supported image loads it into the workspace
- [ ] Selecting two images loads baseline and candidate slots
- [ ] Unsupported file types are rejected gracefully (no crash; user-visible feedback if applicable)

### Drag and drop

- [ ] Dragging one supported image onto the window loads it
- [ ] Dragging baseline + candidate images creates or fills a comparison
- [ ] Dropping unsupported files only does not crash the app

### Comparison views

- [ ] `1` — side-by-side view shows both images aligned
- [ ] `2` — split-screen view shows the draggable divider
- [ ] `3` — difference view highlights changed pixels
- [ ] Switching modes preserves the loaded images

### Zoom and navigation

- [ ] `Ctrl + 0` fits images to the window
- [ ] `Ctrl + 1` shows actual size (100%)
- [ ] `Ctrl + mouse wheel` zooms in and out smoothly
- [ ] `Shift + mouse wheel` scrolls horizontally when zoomed in

### Refresh

- [ ] `F5` refreshes source images for the current comparison
- [ ] `Ctrl + F5` refreshes source images for the current project

### Rendering and stability

- [ ] Large/high-resolution images render without obvious corruption
- [ ] Repeated open → compare → switch view → zoom cycles stay responsive
- [ ] Closing and relaunching the app does not leave orphan processes

## Known Windows caveats

- Packaging is prerelease: CI publishes `difflection-win-x64.zip`; macOS assets are not produced yet.
- Difflection uses Avalonia `12.0.999-cibuild0064469-alpha` for Wayland drag-and-drop fixes; watch for platform-specific rendering or input quirks on native Windows.
- Snapshot baselines in CI are generated on Linux — visual pixel parity with Linux is informative but not a release gate for Windows smoke tests.

## Reporting issues

When filing a Windows validation bug, include:

- Windows version and display scaling
- Release tag or commit SHA
- Steps from the checklist that failed
- Sample image dimensions/formats (if relevant)
- Screenshot or screen recording when UI rendering is involved
