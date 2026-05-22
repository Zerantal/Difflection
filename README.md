# Difflection

Difflection is a lightweight desktop image comparison tool built with Avalonia and .NET. It is designed for quick visual review of two static images, with side-by-side and split-screen comparison modes.

The current alpha is aimed at developers, designers, and testers who need a simple local tool for checking image changes without setting up a full visual-regression workflow.

Difflection supports quick visual comparison of two static images using side-by-side, split-screen, and difference highlighting views.

![Difflection side-by-side comparison](docs/images/difflection-side-by-side.png)

![Difflection split-screen comparison](docs/images/difflection-split-screen.png)

![Difflection split-screen comparison](docs/images/difflection-diff-screen.png)

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
dotnet run --project Difflection.Desktop/Difflection.Desktop.csproj
```

For Rider, use the launch profiles in `Difflection.Desktop/Properties/launchSettings.json` and `Difflection.Browser/Properties/launchSettings.json`.

## Browser Host

An initial Avalonia WebAssembly host lives in `Difflection.Browser`.

The project currently builds as part of the solution. Running or publishing it as WebAssembly requires the .NET `wasm-tools` workload:

```bash
dotnet workload restore Difflection.Browser/Difflection.Browser.csproj
dotnet run --project Difflection.Browser/Difflection.Browser.csproj -p:RuntimeIdentifier=browser-wasm
```

The browser host is an early scaffold. The next browser-specific work is adapting image file/drop behavior for browser sandbox constraints.

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
