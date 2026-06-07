# macOS release testing checklist

Use this checklist when validating a macOS build before calling a release ready. It focuses on the desktop app (`Difflection.Desktop`).

## Prerequisites

- macOS 12+ on Apple Silicon (arm64) or Intel (use the matching runtime if publishing both)
- A tagged release asset `difflection-osx-arm64.tar.gz` from [GitHub Releases](https://github.com/Zerantal/Difflection/releases), or a local publish:

```bash
dotnet publish Difflection.Desktop/Difflection.Desktop.csproj \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --output ./artifacts/osx-arm64
```

- Two or more PNG/JPEG test images

## Setup notes

- Extract the tarball and run `./Difflection.Desktop` from the publish folder.
- Gatekeeper may block unsigned prerelease binaries. For local validation only: **System Settings → Privacy & Security → Open Anyway**, or `xattr -dr com.apple.quarantine Difflection.Desktop`.
- Release CI cross-compiles `osx-arm64` on Ubuntu; always smoke-test the published tarball, not only a dev `dotnet run` build.
- Code signing and notarization are not automated yet — document any manual steps used during validation.

## Verification checklist

### Launch and shell

- [ ] App starts from the extracted publish folder
- [ ] Main window renders toolbar, comparison stage, and empty-state hints
- [ ] Light and dark mode (System Settings → Appearance) render correctly

### Open files and drag-and-drop

- [ ] `Cmd + O` opens the file picker and loads supported images
- [ ] Dragging one or two supported images onto the window loads a comparison
- [ ] Unsupported drops fail gracefully (no crash)

### Comparison workflow

- [ ] Side-by-side (`1`), split-screen (`2`), and difference (`3`) modes work
- [ ] `Cmd + 0` / `Cmd + 1`, zoom wheel, and refresh shortcuts (`F5`, `Cmd + F5`) behave as documented in README

### Stability

- [ ] Large images render without corruption
- [ ] Quit and relaunch leaves no orphan processes

## Reporting issues

Include macOS version, Apple Silicon vs Intel, release tag, failing checklist steps, and screenshots when UI rendering is involved.
