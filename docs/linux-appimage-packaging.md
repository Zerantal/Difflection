# Linux AppImage packaging

Difflection ships Linux tarballs from tagged releases. This document describes how to produce a portable **AppImage** for desktop users who prefer a single executable artifact.

## Prerequisites

- Linux x86_64 (build and smoke-test on the same major distro when possible)
- .NET 10 SDK
- `curl` and `bash`
- FUSE (`libfuse2`/`libfuse2t64`) to build with `appimagetool` and to run AppImages on the host

## Quick build

From the repository root:

```bash
chmod +x scripts/build-appimage.sh
./scripts/build-appimage.sh
```

Output: `artifacts/difflection-x86_64.AppImage`

The script:

1. Publishes `Difflection.Desktop` for `linux-x64` (self-contained single file)
2. Stages an AppDir (`AppRun`, `.desktop` entry, binary)
3. Downloads `appimagetool` on first use
4. Writes the AppImage under `artifacts/`

## Reuse an existing publish folder

After `dotnet publish` or a CI artifact extract:

```bash
PUBLISH_DIR=./artifacts/linux-x64 ./scripts/build-appimage.sh --skip-publish
```

## Smoke test

```bash
chmod +x artifacts/difflection-x86_64.AppImage
./artifacts/difflection-x86_64.AppImage
```

Verify:

- [ ] Main window launches
- [ ] `Ctrl + O` opens files
- [ ] Drag-and-drop loads supported images
- [ ] Side-by-side / split / difference modes switch correctly

If FUSE is unavailable, extract with `--appimage-extract` and run `squashfs-root/AppRun`.

## Maintainer notes

- AppImages are built from the same publish flags as `difflection-linux-x64.tar.gz` in `.github/workflows/release.yml`.
- Code signing is not configured — Gatekeeper-style warnings do not apply on Linux, but some desktops may prompt before first launch.
- Wayland drag-and-drop depends on the Avalonia CI alpha noted in README; validate on your target compositor.
- To attach an AppImage to a tagged release, run the script locally or add a CI step after the Linux publish job and upload `difflection-x86_64.AppImage` alongside the tarball.

## Troubleshooting

| Symptom | Check |
|--------|--------|
| `dlopen` / GLIBC errors | Build on an older baseline (e.g. Ubuntu 22.04) or publish from CI |
| AppImage does not start | Run from terminal; ensure FUSE/`libfuse2` is installed |
| Blank window | Confirm publish used `--self-contained true` and bundled native libs |
