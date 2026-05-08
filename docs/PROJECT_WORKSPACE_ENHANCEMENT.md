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

The MVP UI now has the right structural pieces, but it still feels more like a feature scaffold than a persistent workspace. Before refining color or visual styling, the next pass should focus on information architecture, interaction clarity, and making the project/comparison/image relationships obvious.

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

3. Rework the image set area around images as primary objects.

   The image set should be the authoritative place where images are managed. Reference and candidate should be roles assigned to images, not separate fixed slots. Replace the duplicated role summary cards plus table-style image list with a thumbnail strip or grid where each image card shows:

   - Thumbnail.
   - Label and source name.
   - Reference and candidate role badges.
   - Set as Reference action.
   - Set as Candidate action.
   - Delete action.
   - Monitoring status or action, when available.

4. Move destructive project and comparison actions out of tiny minus buttons.

   The current add/delete sidebar controls are functional but too terse for destructive actions. Keep add actions easily available, but move delete and other row-specific actions into contextual menus or explicit row actions with confirmation. This reduces accidental data loss and gives the sidebar more room to breathe.

5. Make sidebar list rows richer.

   Project and comparison rows currently show only names. Add lightweight metadata so the workspace remains navigable with real data:

   - Comparison count per project.
   - Image count per comparison.
   - Optional active/empty status.
   - Optional monitoring indicator.

6. Promote comparison controls into a proper tool row.

   View mode, split ratio, zoom, fit, and reset controls should live together in a consistent comparison toolbar. The current top tabs plus local zoom controls split related controls across separate bands. A single tool row will make comparison actions easier to scan and extend.

7. Clarify comparison stage affordances.

   The comparison stage works visually, but its interactions are mostly implicit. Add subtle pane headers or overlays for Reference and Candidate, especially in side-by-side mode. Improve the drag/drop affordance so the stage clearly communicates where images can be dropped and what will happen.

8. Handle Settings before exposing it.

   The Settings button should either open a real settings surface or be removed/disabled until settings exist. Inert controls make the MVP feel unfinished.

9. Refresh UI snapshots after the structure is settled.

   The current snapshot baselines represent the pre-sidebar shell and no longer match the rendered workspace UI. After the polish pass settles the layout, update the snapshot baselines so future UI regressions are meaningful.

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
