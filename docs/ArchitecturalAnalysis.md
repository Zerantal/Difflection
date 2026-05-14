# Architectural Analysis

This document captures a code-quality and architecture review of the current Difflection codebase, focused on Avalonia composition, code generation opportunities, duplication, unused code, and maintainability.

## Summary

The application is compact and already has a useful separation between models, storage, monitoring, views, and view models. The main pressure points are concentrated in the UI layer:

- `MainView.axaml` combines most of the app shell, toolbar, sidebar, workspace header, empty state, comparison stage host, image-set panel, item templates, and local styling in one file.
- `MainView.axaml.cs` owns a broad mix of UI orchestration, commands, drag/drop, file picker, rename workflows, confirmation, monitoring lifecycle, zoom actions, and selection refresh behavior.
- `WorkspaceNavigatorViewModel` performs a lot of manual property notification fan-out and rebuilds row view models frequently.
- Several custom Avalonia controls manually declare styled properties that are good candidates for Avalonia source generation.
- Some style and visual-state concerns currently live in view models rather than AXAML styling.

## High-Value Improvements

### Split `MainView.axaml`

`Difflection/Views/MainView.axaml` is the largest architectural hotspot. It is currently responsible for several distinct UI regions and a large local style block.

Recommended split:

- `Views/Shell/MainView.axaml`
- `Views/Shell/TopToolbar.axaml`
- `Views/Workspace/WorkspaceSidebar.axaml`
- `Views/Workspace/WorkspaceHeader.axaml`
- `Views/ImageSet/ImageSetPanel.axaml`
- `Views/ImageSet/ImageSetCard.axaml`
- `Styles/Theme.axaml`
- `Styles/Buttons.axaml`
- `Styles/Text.axaml`
- `Styles/ImageSet.axaml`

This would make each AXAML file easier to reason about, reduce `x:Name` coupling in the root view, and allow styles/templates to be reused deliberately.

### Move Local Styles Into Resource Dictionaries

`MainView.axaml` contains many local selectors for buttons, text, list boxes, badges, and image-set cards. These should move into `Application.Styles` via resource dictionaries.

Good first candidates:

- Toolbar/header/icon buttons.
- Sidebar list and list item styles.
- Inline text box styles.
- Workspace title/detail text styles.
- Image-set card, thumbnail, role badge, and metadata text styles.

Likely unused styles should be removed during this pass:

- `Button.toolbarPrimary`
- `Button.toolbarSecondary`
- `Button.imageActionButton`

### Reduce `MainView.axaml.cs` Responsibilities

`MainView.axaml.cs` currently handles several workflows that can be isolated:

- Project/comparison inline rename commit/cancel/focus/select-all.
- Delete confirmation and deletion dispatch.
- Refresh source-image actions.
- File picker and drag/drop file extraction.
- Image-set expanded/collapsed state.
- Image-change monitor lifecycle.
- Toolbar view-mode and zoom actions.

Recommended direction:

- Move command-shaped actions into view models with `[RelayCommand]`.
- Keep code-behind for view-specific integration only, such as file picker APIs that require a `TopLevel`.
- Introduce small attached behaviors or controls for repeatable UI interaction patterns.
- Move monitor lifecycle into a service or app-level coordinator so the root view does not own infrastructure state.

### Extract Inline Rename Behavior

Project and comparison rename handling is duplicated in `MainView.axaml.cs`:

- Lost focus commits the current draft.
- Enter commits.
- Escape cancels.
- The text box focuses and selects all when editing starts.

Create either:

- An `InlineRenameTextBox` control, or
- An attached behavior such as `InlineRename.CommitCommand`, `InlineRename.CancelCommand`, and `InlineRename.FocusWhenVisible`.

The row view models could implement a small common interface:

```csharp
public interface IInlineRenameItem
{
    bool IsEditing { get; }
    string DraftName { get; set; }
    void CancelEdit();
}
```

The view model can still own persistence-specific commit commands.

### Simplify `WorkspaceNavigatorViewModel` Notifications

`WorkspaceNavigatorViewModel` already uses CommunityToolkit attributes in places, but it still has broad manual notification methods such as `NotifySelectedStateChanged`, `NotifySelectedComparisonStateChanged`, and `NotifyWorkspaceStateChanged`.

Recommended changes:

- Add `[NotifyPropertyChangedFor]` attributes to `SelectedComparisonRow`, matching the derived properties it affects.
- Keep manual `OnPropertyChanged` calls only for properties affected by collection mutations or model mutations.
- Replace broad notification fan-out with narrower helpers grouped by mutation type.
- Consider making row collections incrementally updated rather than cleared and rebuilt.

This should reduce missed notification risk and make changes easier to test.

### Avoid Rebuilding Row View Models So Often

`RefreshProjectRows` and `RefreshComparisonRows` clear and recreate row view models. That makes selection repair more complex and loses row object identity.

Options:

- Update `ProjectRows` and `SelectedProjectComparisonRows` incrementally.
- Introduce a small collection synchronization helper keyed by model `Id`.
- Use observable model collections if the domain model can support that cleanly.

This is lower priority than splitting the view, but it would simplify selection state and reduce UI churn.

### Use Avalonia Source Generation For Styled Properties

Several custom controls manually declare Avalonia styled properties and CLR wrappers:

- `Views/RuledImagePane.axaml.cs`
- `Views/RuledSplitImagePane.axaml.cs`
- `Views/PixelRuler.cs`

The project already references Avalonia analyzers/generators. Where supported by the current Avalonia 12 package, these properties should move to generated styled-property attributes.

Good candidates:

- `ImageSource`, `ZoomScale`, `SurfaceWidth`, `SurfaceHeight` on `RuledImagePane`.
- `LeftImage`, `RightImage`, `ZoomScale`, `SurfaceWidth`, `SurfaceHeight` on `RuledSplitImagePane`.
- `Orientation`, `Mode`, `ZoomScale`, ruler segment properties, and brush properties on `PixelRuler`.

Keep manual registration where the control needs custom owner metadata, inherited properties, coercion, validation, or explicit `AffectsRender`/`AffectsMeasure` behavior that the generator cannot express clearly.

### Move Visual State Out Of View Models

`ComparisonImageSetItemViewModel` exposes Avalonia brushes for baseline/candidate button backgrounds and borders.

Prefer exposing semantic state:

- `IsReference`
- `IsCandidate`
- `CanSetReference`
- `CanSetCandidate`
- Possibly a role enum or CSS-like classes from the view layer.

Then style the controls in AXAML selectors. This keeps view models more platform-neutral and avoids duplicating color constants across code and markup.

### Remove Or Finish Placeholder Code

Likely unused or incomplete code found during review:

- `Button.toolbarPrimary`, `Button.toolbarSecondary`, and `Button.imageActionButton` styles appear unused.
- The Settings toolbar button has no command or click handler.
- `MainView_OnSizeChanged` calls `UpdateImageSetHeightLimit`, but that method currently only clears `MaxHeight`.
- `ComparisonStage.LoadDroppedFilesAsync` accepts `preferredSlot`, but the parameter is not used.
- `ComparisonStage.OpenFilePickerAndLoadAsync` and `ComparisonStage.LoadBrowserDroppedFilesAsync` may overlap with `MainView` responsibilities. Keep one owner for file loading interactions.

Each item should either be removed or connected to a real feature.

## Suggested Refactor Order

1. ~~Extract `ImageSetPanel`, `WorkspaceSidebar`, and `TopToolbar` from `MainView.axaml`.~~
2. ~~Move shared styles into resource dictionaries and delete unused style selectors.~~
3. ~~Convert repeated code-behind flows into commands or small attached behaviors, starting with inline rename.~~
4. Move visual-state brushes out of `ComparisonImageSetItemViewModel` and into styles.
5. Simplify `WorkspaceNavigatorViewModel` notifications and row synchronization.
6. Convert manual Avalonia styled properties to generated styled-property attributes where the current Avalonia package supports it cleanly.
7. Remove placeholder/unused code after the component split makes ownership clearer.

## Verification Notes

The core app project was checked with:

```bash
dotnet build Difflection/Difflection.csproj
```

That build succeeded with no warnings or errors.

The full solution build exited early in the sandbox with no diagnostics:

```bash
dotnet build Difflection.sln
```

Before undertaking larger refactors, re-check the full solution build outside the sandbox or investigate why the solution build returns failure without diagnostics in this environment.
