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
dotnet run --project Difflection.Desktop/Difflection.Desktop.csproj
```

## UI Snapshots

Snapshot baselines are stored in `Difflection.Tests/UI/Baselines`.

Normal test runs write latest actual screenshots to `Difflection.Tests/UI/Artifacts`.

When a UI change is intentional, update baselines with:

```bash
UPDATE_SNAPSHOTS=1 dotnet test
```

Review the changed baseline images before committing them.

## Contribution Scope

Small, focused pull requests are welcome.

For larger changes, please open an issue first so the approach can be discussed before implementation work begins.

Good candidates for contributions include:

- Bug fixes
- Documentation improvements
- Small UI polish improvements
- Test coverage improvements
- Packaging/release improvements
- Platform-specific fixes

Please avoid large rewrites, broad refactors, or major workflow changes without prior discussion.

## Working on Issues

If you would like to work on an issue, please leave a comment first.

Issues with an active assignee or a contributor who has already indicated they are working on them should generally be considered reserved.

If there has been no activity for an extended period, feel free to ask whether the issue is available.

## Review and Merge Policy

All changes should go through pull requests.

Pull requests should include:

- A clear description of the problem and solution
- A linked issue where appropriate
- Passing local tests
- Updated snapshot baselines for intentional visual changes
- Documentation updates for user-facing behaviour changes

The maintainer may squash commits when merging to keep the project history readable.