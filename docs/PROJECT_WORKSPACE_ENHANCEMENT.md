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
  SaveProjectAsync(project)
  DeleteProjectAsync(projectId)
  SaveImageAsync(projectId, comparisonId, image)
  LoadImageAsync(imageId)
  DeleteImageAsync(imageId)
```

The desktop implementation can store project metadata and copied images on disk. For example:

```text
Difflection Projects/
  projects.json
  project-{id}/
    project.json
    comparisons/
      comparison-{id}.json
    images/
      image-{id}.png
      image-{id}.jpg
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

1. Add the project, comparison, and image data model.
2. Centralize reference/candidate state transition rules.
3. Add the storage interface and desktop local file storage implementation.
4. Add project and comparison CRUD.
5. Add image CRUD and labeling.
6. Add reference/candidate reassignment behavior.
7. Add sidebar navigation for projects and comparisons.
8. Add file monitoring and version capture.

## Main Recommendation

The overall direction is sound. The most important early decision is to keep persistence behind a storage interface, with desktop local file storage as the primary implementation. The web version can later use a DB/blob storage backend without forcing changes into the core project and comparison model.
