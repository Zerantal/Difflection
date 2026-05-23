# Difflection

Difflection is a cross-platform desktop tool for visual regression review and static image comparison, built with Avalonia and .NET.

It is designed for developers and testers who need a lightweight way to inspect UI snapshot changes without setting up a full visual-regression pipeline.

Features include:

- Side-by-side image comparison
- Split-screen comparison with draggable divider
- Difference highlighting mode
- Zoomable high-resolution inspection
- Snapshot-tested UI with Avalonia headless tests

*Side-by-side Comparison*
![Difflection side-by-side comparison](docs/images/difflection-side-by-side.png)

*Split screen comparison*
![Difflection split-screen comparison](docs/images/difflection-split-screen.png)

*Difference highlighting*
![Difflection difference highlighting comparison](docs/images/difflection-diff-screen.png)

## Status  
This project is currently in an early alpha state. The core comparison workflow is usable, but packaging, broader file handling, accessibility polish, and error handling are still being refined.

## Technical Highlights

- Avalonia cross-platform desktop UI
- Snapshot-based UI testing
- Avalonia headless integration tests
- WebAssembly browser host scaffold
- Cross-platform .NET 10 architecture

## Downloads

Prebuilt binaries are available from the GitHub Releases [page](https://github.com/Zerantal/Difflection/releases).

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

## Roadmap

- Add first packaged release.
- Add keyboard shortcuts for common view and zoom actions.
- Add more comparison modes if useful.
- Add directory comparison workflows
- Add CI-oriented regression review tooling

## Contributing

Contributions are welcome while the project is still early. See `CONTRIBUTING.md` for local setup and development notes.

## License

Difflection is licensed under the MIT License. See `LICENSE`.
