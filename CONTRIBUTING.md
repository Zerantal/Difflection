# Contributing

Thanks for considering a contribution to Difflection.

## Local Setup

Requirements:

- .NET 10 SDK
- A platform supported by Avalonia

Useful commands:

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project Difflection/Difflection.csproj
```

## UI Snapshots

Snapshot baselines are stored in `Difflection.Tests/UI/Baselines`.

Normal test runs write latest actual screenshots to `Difflection.Tests/UI/Artifacts`.

When a UI change is intentional, update baselines with:

```bash
UPDATE_SNAPSHOTS=1 dotnet test
```

Review the changed baseline images before committing them.

## Pull Requests

- Keep changes focused.
- Include tests for behavior changes where practical.
- Update snapshots only when the visual change is intentional.
- Run `dotnet test` before opening a pull request.
