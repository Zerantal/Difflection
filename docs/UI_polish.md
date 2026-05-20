# UI Polish Ideas

## Goal

Difflection already has the right broad direction for an image comparison tool: dark, quiet, dense, and work-focused. The next styling pass should improve hierarchy, reduce color ambiguity, and make the interface feel more deliberate without pulling attention away from the images.

This document lists small experiments that can be tried independently.

## Current Impression

The app feels like a practical inspection workbench. That is good. The dark neutral palette keeps image content prominent, and the layout avoids marketing-style decoration.

The main visual weaknesses are:

- Several dark surfaces are close in value, so hierarchy can feel flat.
- Orange currently acts as both candidate role color and general accent.
- Borders are doing much of the separation work.
- The selected comparison row can read like a role state because it uses the warm accent background.
- Some controls feel heavier than necessary because buttons, panels, borders, and labels all have similar contrast.

## Palette Experiments

### Experiment 1: Separate App Accent From Candidate

Keep candidate orange, but stop using orange as the generic selected/action accent.

Possible mapping:

- Baseline: cyan/blue
- Candidate: orange
- Generic selection/accent: cool neutral blue-gray

Example token direction:

```text
AccentBrush           #AEB4BC
AccentSubtleBrush     #242A30
SelectedBackground    #242A30
CandidateBrush        #F97316
CandidateSubtleBrush  #302012
BaselineBrush         #00AFFF
BaselineSubtleBrush   #0C2530
```

Expected effect:

- Candidate keeps its role identity.
- Selected navigation rows no longer imply candidate/warning state.
- The UI feels calmer because orange appears only when candidate or comparison-result emphasis is intended.

### Experiment 2: Increase Surface Separation

The current dark surfaces are visually close. Try making each layer more intentional.

Example direction:

```text
AppBackgroundBrush        #151515
SidebarBackgroundBrush    #171717
WorkspaceBackgroundBrush  #0D0D0D
PanelBackgroundBrush      #181818
SurfaceBackgroundBrush    #131313
SurfaceRaisedBrush        #242424
ImageCanvasBrush          #050505
ImageViewportFillBrush    #2C2C2C
```

Expected effect:

- The stage remains deepest.
- Sidebars and panels feel connected but still distinct.
- Floating controls stand out without needing stronger borders.

### Experiment 3: Softer Borders, Stronger Surfaces

Use surface contrast before border contrast. Reduce some border strength after surface values are clearer.

Example direction:

```text
BorderSubtleBrush   #282828
BorderDefaultBrush  #333333
BorderStrongBrush   #3D3D3D
PanelBorderBrush    #2A2A2A
```

Expected effect:

- Less grid-like visual noise.
- Image and revision content feels more prominent.
- Panels still separate, but with less hard outlining.

## Component Polish

### Selected Comparison Row

Current selected rows use a warm background. Consider a neutral selected state:

```text
SelectedBackgroundBrush  #252A30
```

Keep warning/review states warm, and keep role states role-colored.

Expected effect:

- Selection reads as navigation state.
- Orange remains meaningful for candidate or metric emphasis.

### View Mode Buttons

The active view-mode button currently uses the generic accent. If orange remains candidate-owned, active view mode should move to neutral/cool styling.

Example:

```text
Button.viewModeButton.active background  SelectedBackgroundBrush
Button.viewModeButton.active border      CheckedBorderBrush
Button.viewModeButton.active foreground  TextPrimaryBrush
```

Expected effect:

- Active tool state is clear but not loud.
- Orange role/state meaning stays cleaner.

### Difference Controls

Difference mode currently benefits from a visible accent, but the opacity value could use a dedicated difference color instead of candidate orange.

Possible direction:

```text
DifferenceBrush        #D7E8F5
DifferenceSubtleBrush  #26313A
```

Expected effect:

- Difference controls feel related to comparison analysis, not candidate role.

### Image Channel Panel

The channel panel is useful but visually heavy. Try reducing border prominence while keeping role labels strong.

Ideas:

- Keep role label colors saturated.
- Make channel frame borders subtler.
- Use role color only on the label and active thumbnail, not the whole frame.

Expected effect:

- Less visual competition with thumbnails.
- Role identity remains clear.

### Thumbnail Overlays

Thumbnail metadata overlays are functional but dense. Try slightly softer overlay contrast:

```text
ThumbnailOverlayBrush     #050505C8
ThumbnailTagOverlayBrush  #101010D8
```

Expected effect:

- Metadata remains readable.
- Thumbnails feel less boxed-in.

## Typography And Density

### Reduce Semibold Overuse

Many labels use `SemiBold`, which gives the app a sturdy but slightly heavy feel.

Try:

- Keep `SemiBold` for project title, selected row text, role labels, and active state text.
- Use regular weight for metadata, counts, and secondary hints.

Expected effect:

- Better text hierarchy.
- Less visual noise in dense areas.

### Toolbar Labels

View mode buttons use text labels, which is appropriate. Keep them compact, but consider slightly less vertical padding if the toolbar feels tall after color changes.

Possible direction:

```text
viewModeButton Padding  9,6
```

## Role Color Guidance

Keep role colors consistent:

- Baseline: blue/cyan
- Candidate: orange
- Difference: separate analysis color or neutral accent
- Warning/review: amber/brown
- Danger/delete: red
- Selection/navigation: neutral or cool gray

Avoid using candidate orange for ordinary selection unless the selected thing is specifically candidate-related.

## Suggested Trial Order

1. Try neutral/cool selected state first.
2. Separate generic accent from candidate orange.
3. Adjust surface contrast.
4. Soften borders.
5. Review image channel panel after the first four changes.
6. Tune typography weights only after the color hierarchy feels right.

## What To Avoid

- Do not brighten the whole app; the dark inspection surface is a strength.
- Do not add gradients, decorative backgrounds, or large visual treatments.
- Do not introduce too many accent colors.
- Do not make the image canvas lighter than necessary; it should remain visually behind the image content.
- Do not reduce contrast on ruler labels or role indicators until the app is tested with real image sets.

## Evaluation Checklist

Use a few real comparison sets and check:

- Can baseline and candidate be identified instantly?
- Does selected navigation state look different from role state?
- Does the image stage remain the main focus?
- Are thumbnails readable without overpowering the stage?
- Are borders helping hierarchy, or creating visual noise?
- Does the UI still feel dense and efficient?
