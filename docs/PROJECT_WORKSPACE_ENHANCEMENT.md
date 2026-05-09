# Project Workspace Enhancement Proposal

This enhancement would turn Difflection from a one-off comparison tool into a persistent workspace for managing projects, image comparisons, and image sets.

## Data Model

A useful data model would be:

```text
Project
  id
  name
  comparisons: ComparisonSet[]

ComparisonSet
  id
  name
  images: ImageAsset[]
  referenceImageId?: string
  candidateImageId?: string

ImageAsset
  id
  label
  sourceName
  mimeType
  dataUrl | blobKey
  addedAt
  originalFileMetadata?
  monitoredFileHandle?
```

The reference and candidate rules should be explicit:

- If a comparison has zero images, it has no reference and no candidate.
- If a comparison has one image, that image is automatically the reference and there is no candidate.
- If a comparison has two or more images, it has one reference and one candidate, and they must be different images.
- Deleting an image should automatically repair the comparison state.
- Setting the candidate to the current reference should either be disallowed or perform the swap behavior described below.

These rules should be centralized in model/update functions rather than scattered across UI components.

## Reference And Candidate Behavior

Each image comparison contains a set of images.

One image in the set is designated as the reference, provided there is at least one image. The reference image can be changed to another image in the set at any time.

One image in the set is designated as the candidate, provided there are at least two images in the set. The candidate image can also be changed at any time.

If there are only two images in the set, reassigning the candidate label to the current reference should swap the reference and candidate designations.

## Storage

Project data should be saved automatically, including copies of all added images. Since Difflection is primarily a desktop app, the preferred approach is to define an agnostic storage interface and provide a local file storage implementation for the desktop version.

A storage abstraction keeps the project model independent from the backing store:

```text
IProjectStorage
  LoadProjectsAsync()
  LoadProjectAsync(projectId)
  SaveProjectAsync(project)
  DeleteProjectAsync(projectId)
  SaveImageAsync(projectId, comparisonSetId, image, content)
  LoadImageAsync(image)
  DeleteImageAsync(image)
```

The desktop implementation can store project metadata and copied images on disk. For example:

```text
Difflection Projects/
  projects/
  {project-id}/
    project.json
    comparisons/
      {comparison-set-id}/
        images/
          {image-id}.png
          {image-id}.jpg
```

The exact file layout can evolve, but the important point is that image data should live as files rather than being embedded directly into one large JSON document. The JSON should reference stored image files by stable IDs or relative paths.

The web version can later provide a different implementation behind the same interface, such as IndexedDB for local browser storage or a DB/blob storage backend for hosted storage.

This gives the app:

- Desktop-first local storage.
- A clean path to browser or backend storage later.
- Easier testing of project persistence behavior.
- Freedom to change the physical storage format without changing UI or model code.

`localStorage` should only be considered for small web UI preferences, not copied image data.

## File Monitoring

The proposed monitoring behavior makes sense if treated as version capture rather than replacing existing images:

- When a monitored reference image changes, add the changed image as a new image and designate it as the reference.
- When a monitored candidate image changes, add the changed image as a new image and designate it as the candidate.

This preserves comparison history and avoids silently mutating previous results.

Useful metadata for monitored images would include:

- Original file path or source name, where available.
- Last modified timestamp.
- Content hash.
- Monitored role: reference, candidate, or none.
- Link to the previous image version.

Hashing is preferable to timestamp-only change detection because some workflows can rewrite files without reliable timestamp changes.

For browser builds, file monitoring has limitations. Normal web apps cannot continuously watch arbitrary local files. With the File System Access API, the app may be able to retain file handles and periodically check for changes, but browser support and permissions vary. In a desktop app, real file watching is more practical.

## UI Direction

A left-hand navigation area with project and comparison lists is a good fit.

One possible layout:

- Left sidebar: project list and comparison list for the selected project.
- Main area: current comparison view, image set thumbnails, reference/candidate indicators, and comparison tools.
- Settings area: monitoring toggle, image labels, and comparison settings.

For the image set, reference and candidate should be displayed as roles assigned to images, not as fixed slots. A thumbnail grid or strip could show all images with role badges and actions:

- Set as Reference
- Set as Candidate
- Rename or Label
- Delete
- Monitor Source

## New Features

The enhancement would introduce:

- Add New Project
- Add New Comparison
- Delete Project
- Delete Comparison
- Delete Image
- Label Image
- Set Image as Reference
- Set Image as Candidate
- Automatically save projects and copied images
- Optionally monitor source images for changes

## Suggested Implementation Order

1. ~~Add the project, comparison, and image data model.~~
2. ~~Centralize reference/candidate state transition rules.~~
3. ~~Add the storage interface and desktop local file storage implementation.~~
4. ~~Add project and comparison CRUD.~~
5. ~~Add image CRUD and labeling.~~
6. ~~Add reference/candidate reassignment behavior.~~
7. ~~Add sidebar navigation for projects and comparisons.~~
8. ~~Add file monitoring and version capture.~~

## Main Recommendation

The overall direction is sound. The most important early decision is to keep persistence behind a storage interface, with desktop local file storage as the primary implementation. The web version can later use a DB/blob storage backend without forcing changes into the core project and comparison model.

## UI Polish Assessment

The MVP UI now has the right structural pieces and several orientation problems have been addressed. The main workspace shows the selected project/comparison context, empty states distinguish the major workspace states, and the view-model refactor has created explicit workspace, image-set, comparison display, tool-state, and status presenters.

The remaining polish work should now focus less on broad architecture and more on making the visible controls match the project model: images should feel like the primary managed objects, row actions should be clearer and safer, and comparison tools should read as a coherent toolbar.

Recommended UI polish sequence:

1. ~~Make the selected project and comparison visible in the main workspace.~~

   ~~The sidebar owns the current project and comparison context, but the main stage does not repeat it. Once users have several projects or comparisons, it will be easy to lose orientation. Add a compact header above the stage showing the selected project, selected comparison, save or monitoring state, and possibly the image count.~~

2. ~~Improve empty states.~~

   ~~The empty UI should distinguish between:~~

   - ~~No projects.~~
   - ~~Project selected but no comparisons.~~
   - ~~Comparison selected but no images.~~
   - ~~One image loaded.~~
   - ~~Two or more images ready to compare.~~

   ~~This matters because the app is now project-oriented, not just a drag-two-images comparison surface.~~

3. ~~Rework the image set area around images as primary objects.~~

   ~~This remains the largest visible gap. The current UI still combines duplicated reference/candidate summary cards with a table-style image list. The image set should be the authoritative place where images are managed. Reference and candidate should be roles assigned to images, not separate fixed slots. Replace the summary cards plus list with a thumbnail strip or grid where each image card shows:~~

   - ~~Thumbnail.~~
   - ~~Label and source name.~~
   - ~~Reference and candidate role badges.~~
   - ~~Set as Reference action.~~
   - ~~Set as Candidate action.~~
   - ~~Delete action.~~
   - ~~Monitoring status or action, when available.~~

4. ~~Move destructive project and comparison actions out of tiny minus buttons.~~

   ~~The current add/delete sidebar controls are functional but too terse for destructive actions, and they currently perform deletion without an explicit confirmation surface. Keep add actions easily available, but move delete and other row-specific actions into contextual menus or explicit row actions with confirmation. This reduces accidental data loss and gives the sidebar more room to breathe.~~

5. Make sidebar list rows richer.

   The row view models now expose lightweight metadata through `DetailText`, but the XAML still renders only the row names. Surface that metadata so the workspace remains navigable with real data:

   - Comparison count per project.
   - Image count per comparison.
   - Optional active/empty status.
   - Optional monitoring indicator.

6. Promote comparison controls into a proper tool row.

   View mode, split ratio, zoom, fit, and reset controls should live together in a consistent comparison toolbar. The current top tabs plus local zoom controls still split related controls across separate bands, and fit/reset behavior is available through stage interactions rather than a visible tool row. A single tool row will make comparison actions easier to scan and extend.

7. Clarify comparison stage affordances.

   The comparison stage works visually, but its interactions are mostly implicit. Add subtle pane headers or overlays for Reference and Candidate, especially in side-by-side mode. Improve the drag/drop affordance so the stage clearly communicates where images can be dropped and what will happen.

8. Handle Settings before exposing it.

   The Settings button should either open a real settings surface or be removed/disabled until settings exist. It is still visible as a toolbar button without a wired action, which makes the MVP feel unfinished.

9. Refresh UI snapshots after the structure is settled.

   The snapshot suite now includes workspace states, image-set states, split-screen, side-by-side, zoomed, and narrow-layout baselines. After the next structural polish pass, refresh the affected baselines through explicit visual review so future UI regressions remain meaningful.

## MainView Refactor

The next architectural cleanup should make `MainView` less dependent on the rendered control tree and move more UI state into explicit, testable view-model state.

Suggested improvements:

1. ~~Replace visual-tree based inline rename with explicit row edit state.~~

   ~~Project and comparison rename mode should be represented by sidebar row state, not by searching for a `TextBox` and toggling its properties. The view should render normal text or an editor based on row state, and commit/cancel through view-model commands or methods.~~

2. ~~Split sidebar item presentation from persisted domain models.~~

   ~~`Project` and `ComparisonSet` should remain storage/domain objects. Sidebar-specific concerns such as selected row, draft rename text, editing state, counts, and row actions should live in lightweight row view models.~~

3. ~~Remove manual sidebar selection synchronization from `MainView`.~~

   ~~Selection should flow through bindable `SelectedProjectRow` and `SelectedComparisonRow` state. Code-behind should not need to set list indices after property changes.~~

4. ~~Move drop/file-add orchestration out of event handlers.~~

   ~~`MainView` can still adapt Avalonia events and storage files, but the workflow decisions should sit behind view-model methods so empty-state drops, toolbar adds, and future image-set drops follow the same path.~~

5. Keep code-behind focused on view adapters.

   Long-term, `MainView.axaml.cs` should mostly handle Avalonia-specific adapters: focus, pointer/keyboard events, file picker integration, and comparison-stage coordination. Business rules and UI state transitions should be owned by the view model.

## MainWindowViewModel Refactor

`MainWindowViewModel` has become the central coordinator for too many concerns. It currently owns workspace navigation, sidebar row state, persistence coordination, image-set editing, transient bitmap display state, comparison tools, monitoring/version capture, empty-state text, and the command surface for the whole main view.

The goal of this refactor should be to split the class along stable responsibility boundaries, reduce observable state synchronization, and make each part easier to test in isolation.

Recommended extraction candidates:

1. Extract comparison display state first.

   Create a `ComparisonDisplayViewModel` for transient rendered comparison state:

   - `LeftImage`
   - `RightImage`
   - `LeftFileName`
   - `RightFileName`
   - `DifferenceStatusText`
   - `StageWidth`
   - `StageHeight`
   - image bitmap loading
   - `RefreshCurrentComparisonImagesAsync`
   - bitmap disposal
   - `ImageDifferenceMetric` usage

   This is the safest first split because it mostly concerns display-only bitmap state, not persisted workspace structure.

2. Extract workspace navigation and sidebar state.

   Create a `WorkspaceNavigatorViewModel` or `ProjectWorkspaceViewModel` for project/comparison navigation:

   - `Projects`
   - `ProjectRows`
   - `SelectedProjectRow`
   - `SelectedProjectComparisonRows`
   - `SelectedComparisonRow`
   - project add/delete/rename
   - comparison add/delete/rename
   - inline rename state
   - row refresh and selection repair

   This boundary is strong because row view models already exist and are explicitly sidebar-facing.

3. Extract comparison image-set operations.

   Create a `ComparisonImageSetViewModel` or image-set workflow service for image domain operations:

   - add image
   - add files/browser files to current comparison
   - delete image
   - label image
   - set reference image
   - set candidate image
   - default comparison name from first image
   - reference/candidate role repair interactions

   This should own image-set workflow rules while the parent VM supplies or references the current project/comparison context.

4. Extract comparison tool state.

   Create a small `ComparisonToolStateViewModel` for comparison mode and zoom state:

   - `SelectedViewMode`
   - `IsSideBySideView`
   - `IsSplitScreenView`
   - `CanUseSplitScreen`
   - `CurrentViewTitle`
   - `ZoomScale`
   - `ZoomText`
   - `SplitPercentageText`
   - `TrySetZoomText`
   - view-mode selection methods

   This is smaller than the other extractions, but it removes a cluster of unrelated observable state from the main workspace VM.

5. Extract workspace status and empty-state presentation.

   Move derived UI copy and visibility policy into a presenter or small VM:

   - `WorkspaceContextTitle`
   - `WorkspaceContextDetail`
   - `WorkspaceActionHint`
   - `ShowWorkspaceActionHint`
   - `ShowMainEmptyState`
   - `MainEmptyStateTitle`
   - `MainEmptyStateMessage`
   - `ShowProjectsEmptyState`
   - `ShowComparisonsEmptyState`

   This keeps UI wording and empty-state policy out of the stateful workspace coordinator.

Observable state simplification opportunities:

1. Prefer row selection as the primary UI selection state.

   The VM currently exposes both domain selection and row selection:

   - `SelectedProject`
   - `SelectedProjectRow`
   - `SelectedComparison`
   - `SelectedComparisonRow`

   Long-term, prefer making row selection primary for the UI and exposing domain objects as derived state:

   ```csharp
   public Project? SelectedProject => SelectedProjectRow?.Project;
   public ComparisonSet? SelectedComparison => SelectedComparisonRow?.Comparison;
   ```

   This would remove much of the bidirectional synchronization in the selection partial methods.

2. Replace left/right display fields with pane view models.

   Consider replacing:

   - `LeftImage`
   - `RightImage`
   - `LeftFileName`
   - `RightFileName`

   with:

   ```csharp
   ComparisonPaneViewModel ReferencePane
   ComparisonPaneViewModel CandidatePane
   ```

   Each pane can own its bitmap, display name, empty state, dimensions, and disposal. This should reduce `HasLeftImage`, `HasRightImage`, width/height, and stage-size notification churn.

3. Centralize notification groups.

   The class currently has many `NotifyPropertyChangedFor` attributes plus manual `OnPropertyChanged` clusters. After extraction, each child VM should own its own notification graph. The parent should avoid broadcasting broad UI-state changes except when child references or workspace selection changes.

Suggested implementation order:

1. ~~Extract `ComparisonDisplayViewModel`.~~
2. ~~Extract `WorkspaceNavigatorViewModel`.~~
3. ~~Extract image-set operations into a child VM or workflow service.~~
4. ~~Simplify selected row/domain state.~~
5. ~~Extract comparison tool state.~~
6. ~~Extract empty-state/status presentation.~~

Avoid doing this as one large rewrite. Each extraction should preserve behavior, run the full test suite, and leave snapshot updates for explicit visual review.
