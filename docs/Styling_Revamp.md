# Styling Revamp

## Summary

Difflection currently has a coherent dark, utilitarian visual direction that fits an image comparison tool. The main issue is not the look itself, but how the theme is implemented: most colors are hardcoded directly in views, styles, and some code-behind. This makes the design harder to maintain, makes role colors easier to drift, and makes future light/high-contrast theme work more expensive than it needs to be.

The recommended revamp is to keep the current dark-first product feel, but introduce semantic theme resources and move state styling out of code-behind where possible.

## Current State

- The application is explicitly dark themed in `Difflection/App.axaml` with `RequestedThemeVariant="Dark"`.
- `Difflection/Styles/Workspace.axaml` contains many useful component-level styles, but most values are raw hex colors.
- Several views also define raw colors directly, including `MainView.axaml`, `WorkspaceSidebar.axaml`, `ImageSetPanel.axaml`, `ComparisonStage.axaml`, `RuledImagePane.axaml`, `RuledSplitImagePane.axaml`, and `SettingsDialog.axaml`.
- `TopToolbar.axaml.cs` applies active view-mode colors in code via `Brush.Parse`.
- `PixelRuler.cs` defines default brushes that duplicate ruler colors also supplied in XAML.
- `ImageViewportFillBrush` already exists as a resource, which is a good pattern to extend.

## Findings

### Role Colors Are Inconsistent

Baseline and candidate colors are not consistently mapped across the interface.

Examples:

- Active baseline thumbnail border uses cyan/blue.
- Active candidate thumbnail border uses orange.
- Baseline badge uses orange.
- Candidate badge uses cyan/blue.
- Baseline channel frame uses a blue-tinted border.
- Candidate channel frame uses an orange/brown-tinted border.

This creates unnecessary cognitive load. Users should be able to learn one color mapping and trust it everywhere.

Recommended mapping:

- Baseline: blue/cyan
- Candidate: orange
- Difference/selected tool state: either the general app accent or a separate difference color

The exact mapping is less important than consistency.

### Theme Values Are Not Centralized

The dark theme depends on many repeated hex values such as `#101010`, `#181818`, `#1B1B1B`, `#262626`, `#3A3A3A`, `#F3F4F6`, and `#9AA1AB`.

Because these are spread through views and styles, a palette adjustment requires touching many unrelated files. It also increases the chance that similar surfaces gradually become visually inconsistent.

The app would benefit from a dedicated theme dictionary with semantic resource names.

### State Styling Is Split Between XAML And Code

`TopToolbar.axaml.cs` manually sets active view-mode button colors:

- Background
- BorderBrush
- Foreground

This bypasses normal styling patterns and makes hover, focus, disabled, and selected states harder to inspect or modify. It would be better to expose selected state through classes or pseudo-classes and style it in XAML.

### Ruler Styling Is Duplicated

Ruler brushes are defined both in `RuledImagePane.axaml`/`RuledSplitImagePane.axaml` and as defaults in `PixelRuler.cs`.

The ruler is a core visual element for this app, so its colors should come from shared theme resources. The control defaults can remain as safe fallbacks, but normal application usage should be resource-driven.

### Component Styles Are Useful But Repetitive

Several button styles share the same visual base:

- `sidebarIconButton`
- `headerIconButton`
- `imageActionIconButton`

They differ mainly by size and context. A base icon-button style plus size/context classes would make behavior and polish easier to keep consistent.

## Recommended Theme Resources

Create a shared resource dictionary such as `Difflection/Styles/Theme.axaml` and include it from `App.axaml` before component styles.

Suggested semantic resources:

- `AppBackgroundBrush`
- `TopBarBackgroundBrush`
- `SidebarBackgroundBrush`
- `WorkspaceBackgroundBrush`
- `PanelBackgroundBrush`
- `SurfaceBackgroundBrush`
- `SurfaceRaisedBrush`
- `ImageViewportFillBrush`
- `ImageCanvasBrush`
- `BorderSubtleBrush`
- `BorderStrongBrush`
- `TextPrimaryBrush`
- `TextSecondaryBrush`
- `TextMutedBrush`
- `TextDisabledBrush`
- `AccentBrush`
- `AccentSubtleBrush`
- `BaselineBrush`
- `BaselineSubtleBrush`
- `CandidateBrush`
- `CandidateSubtleBrush`
- `WarningBrush`
- `WarningSubtleBrush`
- `DangerBrush`
- `DangerSubtleBrush`
- `RulerBackgroundBrush`
- `RulerBorderBrush`
- `RulerMajorTickBrush`
- `RulerMinorTickBrush`
- `RulerTextBrush`

For the first pass, these can preserve the existing dark palette. The main win is moving from literal colors to semantic intent.

## Implementation Plan

1. [x] Add `Styles/Theme.axaml`.
2. [x] Move `ImageViewportFillBrush` from `App.axaml` into the new theme dictionary.
3. [x] Define the semantic brushes listed above using the current palette.
4. [x] Include `Theme.axaml` from `App.axaml`.
5. [x] Replace high-frequency hardcoded colors in `Workspace.axaml` with `{DynamicResource ...}`.
6. [x] Replace view-level shell colors in `MainView.axaml`, `WorkspaceSidebar.axaml`, `ImageSetPanel.axaml`, `ComparisonStage.axaml`, and ruler views.
7. [x] Normalize baseline/candidate colors across badges, channel frames, thumbnail borders, and action states.
8. [x] Move active view-mode button styling out of `TopToolbar.axaml.cs` and into XAML styles.
9. Point `PixelRuler` usage at theme resources; keep code defaults only as fallback values.
10. Run existing UI snapshot tests and update baselines only after visually reviewing the intentional changes.

## Suggested Priorities

### Phase 1: Low-Risk Tokenization

Add theme resources and replace repeated neutral/text/border colors without changing the visible design. This should be mostly mechanical and should not intentionally alter screenshots.

### Phase 2: Role Color Cleanup

Normalize baseline and candidate colors. This will intentionally change parts of the UI, especially badges and active states.

### Phase 3: State Styling Cleanup

Refactor toolbar active states and shared icon button styles so interaction states are fully controlled by styles rather than code-behind brush assignment.

### Phase 4: Theme Expansion

Only after the dark theme is resource-driven, consider adding light theme or high-contrast variants. Light theme support should be deliberate because the app's image inspection surfaces benefit from neutral dark backgrounds.

## Non-Goals

- Do not redesign the app into a marketing-style interface.
- Do not add decorative gradients or large visual treatments that compete with image comparison.
- Do not change layout density unless a specific usability issue is being addressed.
- Do not introduce light theme support until the dark theme is centralized and stable.

## Testing Notes

The project already has UI snapshot baselines under `Difflection.Tests/UI/Baselines`. Styling changes should be verified with the existing UI tests. Any snapshot updates should be reviewed visually to confirm they reflect intentional design changes rather than accidental drift.
