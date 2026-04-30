# Difflection

Difflection is a desktop image comparison tool built with Avalonia. It is aimed at quick static image review with side-by-side and split-screen comparison views.

## Status

This project is currently in an early alpha state. The core comparison workflow is usable, but packaging, broader file handling, accessibility polish, and error handling are still being refined.

## Features

- Load reference and candidate images by file picker or drag and drop.
- Compare images in a side-by-side view.
- Compare images in a split-screen overlay view with a draggable divider.
- Zoom with `Ctrl` + mouse wheel without scrolling the viewport.
- Snapshot-tested UI using Avalonia headless tests.

## Supported Runtime

- .NET 10
- Avalonia `12.0.999-cibuild0064469-alpha`

The app is intended to be cross-platform through Avalonia, but Linux is the current development environment. Windows and macOS packaging have not been completed yet.

## Build And Run

```bash
dotnet restore
dotnet build
dotnet run --project Difflection/Difflection.csproj
```

## Test

```bash
dotnet test
```

UI snapshot baselines live in `Difflection.Tests/UI/Baselines`. Test runs write actual screenshots to `Difflection.Tests/UI/Artifacts`.

To accept updated UI snapshots:

```bash
UPDATE_SNAPSHOTS=1 dotnet test
```

## Known Limitations

- No installer or release packaging yet.
- Image loading errors are not yet surfaced with user-friendly messages.
- Split-screen comparison is only enabled after two images are present.
- Snapshot tests are sensitive to rendering/font changes across environments.

## Roadmap

- Add first packaged release.
- Improve image loading validation and error reporting.
- Add keyboard shortcuts for common view and zoom actions.
- Add more comparison modes if useful.

## Contributing

Contributions are welcome while the project is still early. See `CONTRIBUTING.md` for local setup and development notes.

## License

Difflection is licensed under the MIT License. See `LICENSE`.
