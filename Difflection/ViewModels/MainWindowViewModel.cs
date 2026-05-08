using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Difflection.Models;
using Difflection.Monitoring;
using Difflection.Storage;

namespace Difflection.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string DefaultProjectName = "Untitled Project";
    private const string DefaultComparisonName = "Untitled Comparison";

    private readonly MonitoredImageVersionCapture? _monitoredImageVersionCapture;

    public MainWindowViewModel()
    {
    }

    public MainWindowViewModel(IProjectStorage projectStorage)
    {
        ProjectStorage = projectStorage;
        _monitoredImageVersionCapture = new MonitoredImageVersionCapture(projectStorage);
    }

    public ObservableCollection<Project> Projects { get; } = [];

    public ObservableCollection<ProjectListItemViewModel> ProjectRows { get; } = [];

    public ObservableCollection<ComparisonListItemViewModel> SelectedProjectComparisonRows { get; } = [];

    public IProjectStorage? ProjectStorage { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSideBySideView))]
    [NotifyPropertyChangedFor(nameof(IsSplitScreenView))]
    [NotifyPropertyChangedFor(nameof(CurrentViewTitle))]
    public partial ComparisonViewMode SelectedViewMode { get; set; } = ComparisonViewMode.SideBySide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedProject))]
    [NotifyPropertyChangedFor(nameof(SelectedProjectComparisons))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedProject))]
    [NotifyPropertyChangedFor(nameof(CanAddComparison))]
    [NotifyPropertyChangedFor(nameof(SelectedProjectName))]
    [NotifyPropertyChangedFor(nameof(WorkspaceContextTitle))]
    [NotifyPropertyChangedFor(nameof(WorkspaceContextDetail))]
    [NotifyPropertyChangedFor(nameof(WorkspaceActionHint))]
    [NotifyPropertyChangedFor(nameof(ShowWorkspaceActionHint))]
    [NotifyPropertyChangedFor(nameof(ShowMainEmptyState))]
    [NotifyPropertyChangedFor(nameof(MainEmptyStateTitle))]
    [NotifyPropertyChangedFor(nameof(MainEmptyStateMessage))]
    [NotifyPropertyChangedFor(nameof(ShowComparisonsEmptyState))]
    public partial Project? SelectedProject { get; set; }

    [ObservableProperty]
    public partial ProjectListItemViewModel? SelectedProjectRow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedComparison))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedComparison))]
    [NotifyPropertyChangedFor(nameof(SelectedComparisonImages))]
    [NotifyPropertyChangedFor(nameof(SelectedComparisonName))]
    [NotifyPropertyChangedFor(nameof(SelectedComparisonImageCountText))]
    [NotifyPropertyChangedFor(nameof(WorkspaceContextTitle))]
    [NotifyPropertyChangedFor(nameof(WorkspaceContextDetail))]
    [NotifyPropertyChangedFor(nameof(WorkspaceActionHint))]
    [NotifyPropertyChangedFor(nameof(ShowWorkspaceActionHint))]
    [NotifyPropertyChangedFor(nameof(ShowMainEmptyState))]
    [NotifyPropertyChangedFor(nameof(MainEmptyStateTitle))]
    [NotifyPropertyChangedFor(nameof(MainEmptyStateMessage))]
    [NotifyPropertyChangedFor(nameof(ShowComparisonsEmptyState))]
    public partial ComparisonSet? SelectedComparison { get; set; }

    [ObservableProperty]
    public partial ComparisonListItemViewModel? SelectedComparisonRow { get; set; }

    [ObservableProperty]
    public partial string SplitPercentageText { get; set; } = "50 / 50";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    public partial double ZoomScale { get; set; } = 1.0;

    [ObservableProperty]
    public partial string ZoomText { get; set; } = "100%";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    public partial double StageWidth { get; set; } = 920;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    public partial double StageHeight { get; set; } = 560;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLeftImage))]
    [NotifyPropertyChangedFor(nameof(HasAnyImage))]
    [NotifyPropertyChangedFor(nameof(HasBothImages))]
    [NotifyPropertyChangedFor(nameof(CanUseSplitScreen))]
    [NotifyPropertyChangedFor(nameof(LeftImageWidth))]
    [NotifyPropertyChangedFor(nameof(LeftImageHeight))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    [NotifyPropertyChangedFor(nameof(ShowMainEmptyState))]
    public partial Bitmap? LeftImage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRightImage))]
    [NotifyPropertyChangedFor(nameof(HasAnyImage))]
    [NotifyPropertyChangedFor(nameof(HasBothImages))]
    [NotifyPropertyChangedFor(nameof(CanUseSplitScreen))]
    [NotifyPropertyChangedFor(nameof(RightImageWidth))]
    [NotifyPropertyChangedFor(nameof(RightImageHeight))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageWidth))]
    [NotifyPropertyChangedFor(nameof(SideBySideStageHeight))]
    [NotifyPropertyChangedFor(nameof(ShowMainEmptyState))]
    public partial Bitmap? RightImage { get; set; }

    [ObservableProperty]
    public partial string LeftFileName { get; set; } = "Reference image";

    [ObservableProperty]
    public partial string RightFileName { get; set; } = "Candidate image";

    [ObservableProperty]
    public partial string DifferenceStatusText { get; set; } = "Load two images to compare";

    public bool HasLeftImage => LeftImage is not null;

    public bool HasRightImage => RightImage is not null;

    public bool HasAnyImage => HasLeftImage || HasRightImage;

    public bool HasBothImages => HasLeftImage && HasRightImage;

    public bool HasSelectedProject => SelectedProject is not null;

    public bool HasSelectedComparison => SelectedComparison is not null;

    public bool HasProjects => Projects.Count > 0;

    public bool ShowProjectsEmptyState => !HasProjects;

    public bool ShowComparisonsEmptyState => HasSelectedProject && SelectedProjectComparisonRows.Count == 0;

    public bool CanDeleteSelectedProject => HasSelectedProject;

    public bool CanAddComparison => HasSelectedProject;

    public bool CanDeleteSelectedComparison => HasSelectedComparison;

    public string SelectedProjectName
    {
        get => SelectedProject?.Name ?? string.Empty;
        set
        {
            if (SelectedProject is null) return;
            SelectedProject.Name = NormalizeName(value, DefaultProjectName);
            SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
            FindProjectRow(SelectedProject)?.Refresh();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedProjectComparisons));
            OnPropertyChanged(nameof(WorkspaceContextTitle));
        }
    }

    public string SelectedComparisonName
    {
        get => SelectedComparison?.Name ?? string.Empty;
        set
        {
            if (SelectedProject is null || SelectedComparison is null) return;
            SelectedComparison.Name = NormalizeName(value, DefaultComparisonName);
            SelectedComparison.UpdatedAt = DateTimeOffset.UtcNow;
            SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
            FindComparisonRow(SelectedComparison)?.Refresh();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedProjectComparisons));
            OnPropertyChanged(nameof(WorkspaceContextTitle));
        }
    }

    public IReadOnlyList<ComparisonSet> SelectedProjectComparisons => SelectedProject?.Comparisons.ToArray() ?? [];

    public IReadOnlyList<ImageAsset> SelectedComparisonImages => SelectedComparison?.Images.ToArray() ?? [];

    public void BeginRenameProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (FindProjectRow(project) is { } row)
        {
            BeginRenameProject(row);
        }
    }

    [RelayCommand]
    public void BeginRenameProject(ProjectListItemViewModel projectRow)
    {
        ArgumentNullException.ThrowIfNull(projectRow);

        foreach (var row in ProjectRows)
        {
            if (ReferenceEquals(row, projectRow))
            {
                SelectedProjectRow = row;
                row.BeginEdit();
            }
            else
            {
                row.CancelEdit();
            }
        }
    }

    public void BeginRenameComparison(ComparisonSet comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        if (FindComparisonRow(comparison) is { } row)
        {
            BeginRenameComparison(row);
        }
    }

    [RelayCommand]
    public void BeginRenameComparison(ComparisonListItemViewModel comparisonRow)
    {
        ArgumentNullException.ThrowIfNull(comparisonRow);

        foreach (var row in SelectedProjectComparisonRows)
        {
            if (ReferenceEquals(row, comparisonRow))
            {
                SelectedComparisonRow = row;
                row.BeginEdit();
            }
            else
            {
                row.CancelEdit();
            }
        }
    }

    public async Task CommitProjectRenameAsync(ProjectListItemViewModel row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsEditing)
        {
            return;
        }

        await RenameProjectAsync(row.Project, row.DraftName, cancellationToken);
        row.EndEdit();
    }

    public async Task CommitComparisonRenameAsync(ComparisonListItemViewModel row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsEditing)
        {
            return;
        }

        await RenameComparisonAsync(row.Comparison, row.DraftName, cancellationToken);
        row.EndEdit();
    }

    public void CancelProjectRename(ProjectListItemViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.CancelEdit();
    }

    public void CancelComparisonRename(ComparisonListItemViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.CancelEdit();
    }

    public async Task CommitActiveInlineRenamesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var row in ProjectRows.Where(row => row.IsEditing).ToArray())
        {
            await CommitProjectRenameAsync(row, cancellationToken);
        }

        foreach (var row in SelectedProjectComparisonRows.Where(row => row.IsEditing).ToArray())
        {
            await CommitComparisonRenameAsync(row, cancellationToken);
        }
    }

    public string SelectedComparisonImageCountText
    {
        get
        {
            var imageCount = SelectedComparison?.Images.Count ?? 0;
            return imageCount == 1 ? "1 image" : $"{imageCount} images";
        }
    }

    public string WorkspaceContextTitle
    {
        get
        {
            if (SelectedProject is null)
            {
                return "No project selected";
            }

            return SelectedComparison is null
                ? SelectedProject.Name
                : $"{SelectedProject.Name} / {SelectedComparison.Name}";
        }
    }

    public string WorkspaceContextDetail
    {
        get
        {
            if (SelectedProject is null)
            {
                return "Create or select a project";
            }

            if (SelectedComparison is null)
            {
                return "No comparison selected";
            }

            return $"{SelectedComparisonImageCountText} in image set";
        }
    }

    public string WorkspaceActionHint
    {
        get
        {
            if (!HasProjects)
            {
                return "Create a project to start a workspace.";
            }

            if (SelectedProject is null)
            {
                return "Select a project to continue.";
            }

            if (SelectedComparison is null)
            {
                return "Create a comparison for this project.";
            }

            return SelectedComparison.Images.Count switch
            {
                0 => "Add or drop a reference image.",
                1 => "Add or drop a candidate image.",
                _ => string.Empty
            };
        }
    }

    public bool ShowWorkspaceActionHint => !string.IsNullOrWhiteSpace(WorkspaceActionHint);

    public bool ShowMainEmptyState => !HasAnyImage && SelectedComparison?.Images.Count is null or 0;

    public string MainEmptyStateTitle
    {
        get
        {
            if (!HasProjects)
            {
                return "No projects";
            }

            if (SelectedProject is null)
            {
                return "No project selected";
            }

            if (SelectedComparison is null)
            {
                return "No comparison selected";
            }

            return "No images in this comparison";
        }
    }

    public string MainEmptyStateMessage
    {
        get
        {
            if (!HasProjects)
            {
                return "Create a project, or drop images to create one automatically.";
            }

            if (SelectedProject is null)
            {
                return "Select a project from the sidebar.";
            }

            if (SelectedComparison is null)
            {
                return "Create a comparison, or drop images to create one automatically.";
            }

            return "Add or drop a reference image to begin.";
        }
    }

    public bool CanSetReferenceImage(ImageAsset? image)
    {
        return SelectedComparison is not null
            && image is not null
            && SelectedComparison.Images.Contains(image);
    }

    public bool CanSetCandidateImage(ImageAsset? image)
    {
        return SelectedComparison is not null
            && image is not null
            && SelectedComparison.Images.Count >= 2
            && SelectedComparison.Images.Contains(image);
    }

    public double LeftImageWidth => LeftImage?.PixelSize.Width ?? StageWidth;

    public double LeftImageHeight => LeftImage?.PixelSize.Height ?? StageHeight;

    public double RightImageWidth => RightImage?.PixelSize.Width ?? StageWidth;

    public double RightImageHeight => RightImage?.PixelSize.Height ?? StageHeight;

    public bool IsSideBySideView => SelectedViewMode == ComparisonViewMode.SideBySide;

    public bool IsSplitScreenView => SelectedViewMode == ComparisonViewMode.SplitScreen;

    public bool CanUseSplitScreen => HasBothImages;

    public string CurrentViewTitle => SelectedViewMode switch
    {
        ComparisonViewMode.SplitScreen => "Split screen",
        _ => "Side-by-side"
    };

    public double SideBySideStageWidth => HasBothImages
        ? LeftImageWidth + 16 + RightImageWidth
        : HasLeftImage
            ? LeftImageWidth
            : HasRightImage
                ? RightImageWidth
                : StageWidth;

    public double SideBySideStageHeight => HasBothImages
        ? Math.Max(LeftImageHeight, RightImageHeight)
        : HasLeftImage
            ? LeftImageHeight
            : HasRightImage
                ? RightImageHeight
                : StageHeight;

    public async Task LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        if (ProjectStorage is null)
        {
            return;
        }

        var projects = await ProjectStorage.LoadProjectsAsync(cancellationToken);

        Projects.Clear();

        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        RefreshProjectRows();
        SelectedProject = Projects.FirstOrDefault();
        SelectedComparison = SelectedProject?.Comparisons.FirstOrDefault();
        NotifyWorkspaceStateChanged();
    }

    public async Task<Project> AddProjectAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Name = NormalizeName(name, DefaultProjectName)
        };

        Projects.Add(project);
        RefreshProjectRows();
        SelectedProject = project;
        SelectedComparison = null;
        NotifyWorkspaceStateChanged();

        await SaveProjectAsync(project, cancellationToken);
        return project;
    }

    [RelayCommand]
    public async Task<Project> AddProjectForInlineRenameAsync(CancellationToken cancellationToken = default)
    {
        var project = await AddProjectAsync(cancellationToken: cancellationToken);
        BeginRenameProject(project);
        return project;
    }

    public async Task<bool> DeleteProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var removed = Projects.Remove(project);

        if (!removed)
        {
            return false;
        }

        RefreshProjectRows();
        if (SelectedProject == project)
        {
            SelectedProject = Projects.FirstOrDefault();
            SelectedComparison = SelectedProject?.Comparisons.FirstOrDefault();
        }

        NotifyWorkspaceStateChanged();

        if (ProjectStorage is not null)
        {
            await ProjectStorage.DeleteProjectAsync(project.Id, cancellationToken);
        }

        return true;
    }

    [RelayCommand]
    public Task<bool> DeleteSelectedProjectAsync(CancellationToken cancellationToken = default)
    {
        return SelectedProject is null
            ? Task.FromResult(false)
            : DeleteProjectAsync(SelectedProject, cancellationToken);
    }

    public async Task<ComparisonSet> AddComparisonAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        if (SelectedProject is null)
        {
            throw new InvalidOperationException("A project must be selected before adding a comparison.");
        }

        var comparison = new ComparisonSet
        {
            Name = NormalizeName(name, DefaultComparisonName)
        };

        SelectedProject.Comparisons.Add(comparison);
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        RefreshComparisonRows();
        FindProjectRow(SelectedProject)?.Refresh();
        OnPropertyChanged(nameof(SelectedProjectComparisons));
        SelectedComparison = comparison;
        NotifyWorkspaceStateChanged();

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return comparison;
    }

    [RelayCommand]
    public async Task<ComparisonSet> AddComparisonForInlineRenameAsync(CancellationToken cancellationToken = default)
    {
        var comparison = await AddComparisonAsync(cancellationToken: cancellationToken);
        BeginRenameComparison(comparison);
        return comparison;
    }

    public async Task EnsureProjectAndComparisonAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProject is null)
        {
            await AddProjectAsync(cancellationToken: cancellationToken);
        }

        if (SelectedComparison is null)
        {
            await AddComparisonAsync(cancellationToken: cancellationToken);
        }
    }

    public async Task RenameSelectedProjectAsync(string? name, CancellationToken cancellationToken = default)
    {
        if (SelectedProject is null)
        {
            return;
        }

        await RenameProjectAsync(SelectedProject, name, cancellationToken);
    }

    public async Task RenameSelectedComparisonAsync(string? name, CancellationToken cancellationToken = default)
    {
        if (SelectedProject is null || SelectedComparison is null)
        {
            return;
        }

        await RenameComparisonAsync(SelectedComparison, name, cancellationToken);
    }

    public async Task RenameProjectAsync(Project project, string? name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (!Projects.Contains(project))
        {
            return;
        }

        project.Name = NormalizeName(name, DefaultProjectName);
        project.UpdatedAt = DateTimeOffset.UtcNow;
        FindProjectRow(project)?.Refresh();
        OnPropertyChanged(nameof(SelectedProjectName));
        OnPropertyChanged(nameof(SelectedProjectComparisons));
        OnPropertyChanged(nameof(WorkspaceContextTitle));
        await SaveProjectAsync(project, cancellationToken);
    }

    public async Task RenameComparisonAsync(ComparisonSet comparison, string? name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        if (SelectedProject is null || !SelectedProject.Comparisons.Contains(comparison))
        {
            return;
        }

        comparison.Name = NormalizeName(name, DefaultComparisonName);
        comparison.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveProjectAsync(SelectedProject, cancellationToken);
        FindComparisonRow(comparison)?.Refresh();
        OnPropertyChanged(nameof(SelectedComparisonName));
        OnPropertyChanged(nameof(SelectedProjectComparisons));
        OnPropertyChanged(nameof(WorkspaceContextTitle));
    }

    public async Task<bool> DeleteComparisonAsync(ComparisonSet comparison, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        if (SelectedProject is null)
        {
            return false;
        }

        var removed = SelectedProject.Comparisons.Remove(comparison);

        if (!removed)
        {
            return false;
        }

        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        RefreshComparisonRows();
        FindProjectRow(SelectedProject)?.Refresh();

        if (SelectedComparison == comparison)
        {
            SelectedComparison = SelectedProject.Comparisons.FirstOrDefault();
        }

        OnPropertyChanged(nameof(SelectedProjectComparisons));
        NotifyWorkspaceStateChanged();
        await SaveProjectAsync(SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public Task<bool> DeleteSelectedComparisonAsync(CancellationToken cancellationToken = default)
    {
        return SelectedComparison is null
            ? Task.FromResult(false)
            : DeleteComparisonAsync(SelectedComparison, cancellationToken);
    }

    public async Task<ImageAsset> AddImageAsync(
        string sourceName,
        Stream content,
        string? mediaType = null,
        string? label = null,
        ImageSourceMetadata? originalFileMetadata = null,
        CancellationToken cancellationToken = default)
    {
        if (SelectedProject is null)
        {
            throw new InvalidOperationException("A project must be selected before adding an image.");
        }

        if (SelectedComparison is null)
        {
            throw new InvalidOperationException("A comparison must be selected before adding an image.");
        }

        ArgumentNullException.ThrowIfNull(content);

        var normalizedSourceName = NormalizeSourceName(sourceName);
        var fallbackLabel = NormalizeName(Path.GetFileNameWithoutExtension(normalizedSourceName), normalizedSourceName);
        var image = new ImageAsset
        {
            Label = NormalizeName(label, fallbackLabel),
            SourceName = normalizedSourceName,
            MediaType = mediaType,
            OriginalFileMetadata = originalFileMetadata
        };

        if (ProjectStorage is not null)
        {
            await ProjectStorage.SaveImageAsync(
                SelectedProject.Id,
                SelectedComparison.Id,
                image,
                content,
                cancellationToken);
        }

        RenameDefaultComparisonFromFirstImage(SelectedComparison, image.Label);
        SelectedComparison.AddImage(image);
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return image;
    }

    public async Task<ImageAsset> AddImageAsync(
        IStorageFile file,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        await using var stream = await file.OpenReadAsync();
        var metadata = await ImageSourceMetadataReader.ReadAsync(file, stream, cancellationToken);
        return await AddImageAsync(file.Name, stream, label: label, originalFileMetadata: metadata, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ImageAsset>> AddFilesToCurrentComparisonAsync(
        IEnumerable<IStorageFile> files,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var imageFiles = maxFiles is null
            ? files.ToArray()
            : files.Take(maxFiles.Value).ToArray();

        if (imageFiles.Length == 0)
        {
            return [];
        }

        await EnsureProjectAndComparisonAsync(cancellationToken);

        var addedImages = new List<ImageAsset>(imageFiles.Length);
        foreach (var file in imageFiles)
        {
            var image = await AddImageAsync(file, cancellationToken: cancellationToken);
            addedImages.Add(image);

            if (SelectedComparison?.ReferenceImageId == image.Id)
            {
                await LoadImageAsync(ImageSlot.Left, file);
            }
            else if (SelectedComparison?.CandidateImageId == image.Id)
            {
                await LoadImageAsync(ImageSlot.Right, file);
            }
        }

        return addedImages;
    }

    public async Task<IReadOnlyList<ImageAsset>> AddFilesToCurrentComparisonAfterCommittingRenamesAsync(
        IEnumerable<IStorageFile> files,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        await CommitActiveInlineRenamesAsync(cancellationToken);
        return await AddFilesToCurrentComparisonAsync(files, maxFiles, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageAsset>> AddBrowserFilesToCurrentComparisonAsync(
        IReadOnlyList<string> fileNames,
        IReadOnlyList<byte[]> fileContents,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        ArgumentNullException.ThrowIfNull(fileContents);

        if (fileNames.Count != fileContents.Count)
        {
            return [];
        }

        var files = fileNames
            .Zip(fileContents, (name, bytes) => (Name: name, Bytes: bytes))
            .Take(maxFiles ?? int.MaxValue)
            .ToArray();

        if (files.Length == 0)
        {
            return [];
        }

        await EnsureProjectAndComparisonAsync(cancellationToken);

        var addedImages = new List<ImageAsset>(files.Length);
        foreach (var file in files)
        {
            using var addStream = new MemoryStream(file.Bytes, writable: false);
            var image = await AddImageAsync(file.Name, addStream, cancellationToken: cancellationToken);
            addedImages.Add(image);

            if (SelectedComparison?.ReferenceImageId == image.Id)
            {
                using var displayStream = new MemoryStream(file.Bytes, writable: false);
                await LoadImageAsync(ImageSlot.Left, image.Label, displayStream);
            }
            else if (SelectedComparison?.CandidateImageId == image.Id)
            {
                using var displayStream = new MemoryStream(file.Bytes, writable: false);
                await LoadImageAsync(ImageSlot.Right, image.Label, displayStream);
            }
        }

        return addedImages;
    }

    public async Task<bool> SetImageMonitoringAsync(
        ImageAsset image,
        ImageMonitoringRole monitoringRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (SelectedProject is null || SelectedComparison is null || !SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        if (monitoringRole != ImageMonitoringRole.None)
        {
            var sourcePath = image.OriginalFileMetadata?.Path;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new InvalidOperationException("Image monitoring requires an existing local source file path.");
            }

            image.OriginalFileMetadata = await ImageSourceMetadataReader.ReadAsync(sourcePath, cancellationToken);
        }

        image.MonitoringRole = monitoringRole;
        SelectedComparison.UpdatedAt = DateTimeOffset.UtcNow;
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return true;
    }

    public async Task<ImageAsset?> CaptureMonitoredImageChangeAsync(
        Project project,
        ComparisonSet comparison,
        ImageAsset changedImage,
        CancellationToken cancellationToken = default)
    {
        if (_monitoredImageVersionCapture is null)
        {
            return null;
        }

        var version = await _monitoredImageVersionCapture.CaptureAsync(project, comparison, changedImage, cancellationToken);

        if (version is null || !ReferenceEquals(project, SelectedProject) ||
            !ReferenceEquals(comparison, SelectedComparison)) return version;
        NotifySelectedComparisonImagesChanged();
        await RefreshCurrentComparisonImagesAsync(cancellationToken);

        return version;
    }

    public async Task<bool> LoadImageAssetAsync(ImageSlot slot, ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (ProjectStorage is null)
        {
            return false;
        }

        await using var stream = await ProjectStorage.LoadImageAsync(image, cancellationToken);
        var bitmap = await CreateBitmapAsync(stream);

        switch (slot)
        {
            case ImageSlot.Left:
                LeftImage = bitmap;
                LeftFileName = GetDisplayName(image);
                break;
            case ImageSlot.Right:
                RightImage = bitmap;
                RightFileName = GetDisplayName(image);
                break;
            default:
                bitmap.Dispose();
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }

        return true;
    }

    public async Task RefreshCurrentComparisonImagesAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedComparison?.ReferenceImage is { } reference)
        {
            await LoadImageAssetAsync(ImageSlot.Left, reference, cancellationToken);
        }
        else
        {
            LeftImage = null;
            LeftFileName = "Reference image";
        }

        if (SelectedComparison?.CandidateImage is { } candidate)
        {
            await LoadImageAssetAsync(ImageSlot.Right, candidate, cancellationToken);
        }
        else
        {
            RightImage = null;
            RightFileName = "Candidate image";
        }
    }

    public async Task<bool> DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (SelectedProject is null || SelectedComparison is null)
        {
            return false;
        }

        var removed = SelectedComparison.RemoveImage(image.Id);

        if (!removed)
        {
            return false;
        }

        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(SelectedProject, cancellationToken);

        if (ProjectStorage is not null)
        {
            await ProjectStorage.DeleteImageAsync(image, cancellationToken);
        }

        return true;
    }

    [RelayCommand]
    public async Task<bool> DeleteImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await DeleteImageAsync(image, cancellationToken))
        {
            return false;
        }

        await RefreshCurrentComparisonImagesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> LabelImageAsync(ImageAsset image, string? label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (SelectedProject is null || SelectedComparison is null || !SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        image.Label = NormalizeName(label, image.SourceName);
        SelectedComparison.UpdatedAt = DateTimeOffset.UtcNow;
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return true;
    }

    public async Task<bool> SetReferenceImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (SelectedProject is null || SelectedComparison is null || !SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        SelectedComparison.SetReferenceImage(image.Id);
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public async Task<bool> SetReferenceImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await SetReferenceImageAsync(image, cancellationToken))
        {
            return false;
        }

        await RefreshCurrentComparisonImagesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetCandidateImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (SelectedProject is null || SelectedComparison is null || !SelectedComparison.Images.Contains(image))
        {
            return false;
        }

        if (SelectedComparison.Images.Count < 2)
        {
            throw new InvalidOperationException("Setting a candidate image requires at least two images in the comparison.");
        }

        SelectedComparison.SetCandidateImage(image.Id);
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedComparisonImagesChanged();

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public async Task<bool> SetCandidateImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        if (!await SetCandidateImageAsync(image, cancellationToken))
        {
            return false;
        }

        await RefreshCurrentComparisonImagesAsync(cancellationToken);
        return true;
    }

    public void SetZoomScale(double zoomScale)
    {
        ZoomScale = Math.Clamp(zoomScale, 0.05, 64.0);
    }

    public bool TrySetZoomText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ZoomText = $"{Math.Round(ZoomScale * 100):0}%";
            return false;
        }

        var trimmed = text.Trim();
        var percentText = trimmed.EndsWith('%') ? trimmed[..^1] : trimmed;

        if (!double.TryParse(percentText, out var percent) || percent <= 0)
        {
            ZoomText = $"{Math.Round(ZoomScale * 100):0}%";
            return false;
        }

        SetZoomScale(percent / 100.0);
        return true;
    }

    public void SelectSideBySideView()
    {
        SelectedViewMode = ComparisonViewMode.SideBySide;
    }

    public void SelectSplitScreenView()
    {
        if (CanUseSplitScreen)
        {
            SelectedViewMode = ComparisonViewMode.SplitScreen;
        }
    }

    public async Task LoadImageAsync(ImageSlot slot, IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        var bitmap = await CreateBitmapAsync(stream);

        switch (slot)
        {
            case ImageSlot.Left:
                LeftImage = bitmap;
                LeftFileName = file.Name;
                break;
            case ImageSlot.Right:
                RightImage = bitmap;
                RightFileName = file.Name;
                break;
            default:
                bitmap.Dispose();
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    public async Task LoadImageAsync(ImageSlot slot, string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var bitmap = await CreateBitmapAsync(stream);

        switch (slot)
        {
            case ImageSlot.Left:
                LeftImage = bitmap;
                LeftFileName = Path.GetFileName(filePath);
                break;
            case ImageSlot.Right:
                RightImage = bitmap;
                RightFileName = Path.GetFileName(filePath);
                break;
            default:
                bitmap.Dispose();
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    public async Task LoadImageAsync(ImageSlot slot, string fileName, Stream stream)
    {
        var bitmap = await CreateBitmapAsync(stream);

        switch (slot)
        {
            case ImageSlot.Left:
                LeftImage = bitmap;
                LeftFileName = Path.GetFileName(fileName);
                break;
            case ImageSlot.Right:
                RightImage = bitmap;
                RightFileName = Path.GetFileName(fileName);
                break;
            default:
                bitmap.Dispose();
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    public void DisposeImages()
    {
        LeftImage = null;
        RightImage = null;
    }

    private static async Task<Bitmap> CreateBitmapAsync(Stream stream)
    {
        if (OperatingSystem.IsBrowser() || !stream.CanSeek)
        {
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            return new Bitmap(buffer);
        }

        return new Bitmap(stream);
    }

    private static string NormalizeName(string? name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    private static string NormalizeSourceName(string? sourceName)
    {
        var fileName = Path.GetFileName(sourceName?.Trim());
        return string.IsNullOrWhiteSpace(fileName) ? "image" : fileName;
    }

    private static string GetDisplayName(ImageAsset image)
    {
        return string.IsNullOrWhiteSpace(image.Label) ? image.SourceName : image.Label;
    }

    private Task SaveProjectAsync(Project project, CancellationToken cancellationToken)
    {
        return ProjectStorage?.SaveProjectAsync(project, cancellationToken) ?? Task.CompletedTask;
    }

    private void NotifySelectedComparisonImagesChanged()
    {
        if (SelectedComparison is not null)
        {
            FindComparisonRow(SelectedComparison)?.Refresh();
        }

        OnPropertyChanged(nameof(SelectedComparisonImages));
        OnPropertyChanged(nameof(SelectedComparisonImageCountText));
        OnPropertyChanged(nameof(WorkspaceContextDetail));
        OnPropertyChanged(nameof(WorkspaceActionHint));
        OnPropertyChanged(nameof(ShowWorkspaceActionHint));
        OnPropertyChanged(nameof(ShowMainEmptyState));
        OnPropertyChanged(nameof(MainEmptyStateTitle));
        OnPropertyChanged(nameof(MainEmptyStateMessage));
    }

    private void NotifyWorkspaceStateChanged()
    {
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(ShowProjectsEmptyState));
        OnPropertyChanged(nameof(ShowComparisonsEmptyState));
        OnPropertyChanged(nameof(WorkspaceActionHint));
        OnPropertyChanged(nameof(ShowWorkspaceActionHint));
        OnPropertyChanged(nameof(ShowMainEmptyState));
        OnPropertyChanged(nameof(MainEmptyStateTitle));
        OnPropertyChanged(nameof(MainEmptyStateMessage));
    }

    private void RefreshProjectRows()
    {
        ProjectRows.Clear();

        foreach (var project in Projects)
        {
            ProjectRows.Add(new ProjectListItemViewModel(project));
        }

        SyncSelectedProjectRow();
        OnPropertyChanged(nameof(ProjectRows));
    }

    private void RefreshComparisonRows()
    {
        SelectedProjectComparisonRows.Clear();

        if (SelectedProject is not null)
        {
            foreach (var comparison in SelectedProject.Comparisons)
            {
                SelectedProjectComparisonRows.Add(new ComparisonListItemViewModel(comparison));
            }
        }

        SyncSelectedComparisonRow();
        OnPropertyChanged(nameof(SelectedProjectComparisonRows));
        OnPropertyChanged(nameof(ShowComparisonsEmptyState));
    }

    private ProjectListItemViewModel? FindProjectRow(Project project)
    {
        return ProjectRows.FirstOrDefault(row => ReferenceEquals(row.Project, project));
    }

    private ComparisonListItemViewModel? FindComparisonRow(ComparisonSet comparison)
    {
        return SelectedProjectComparisonRows.FirstOrDefault(row => ReferenceEquals(row.Comparison, comparison));
    }

    private void SyncSelectedProjectRow()
    {
        var row = SelectedProject is null ? null : FindProjectRow(SelectedProject);
        if (!ReferenceEquals(SelectedProjectRow, row))
        {
            SelectedProjectRow = row;
        }
    }

    private void SyncSelectedComparisonRow()
    {
        var row = SelectedComparison is null ? null : FindComparisonRow(SelectedComparison);
        if (!ReferenceEquals(SelectedComparisonRow, row))
        {
            SelectedComparisonRow = row;
        }
    }

    private void RenameDefaultComparisonFromFirstImage(ComparisonSet comparison, string imageLabel)
    {
        if (comparison.Images.Count > 0 || !string.Equals(comparison.Name, DefaultComparisonName, StringComparison.Ordinal))
        {
            return;
        }

        comparison.Name = NormalizeName(imageLabel, DefaultComparisonName);
        FindComparisonRow(comparison)?.Refresh();
        OnPropertyChanged(nameof(SelectedComparisonName));
        OnPropertyChanged(nameof(SelectedProjectComparisons));
        OnPropertyChanged(nameof(WorkspaceContextTitle));
    }

    private void UpdateStageSize()
    {
        var leftWidth = LeftImage?.PixelSize.Width ?? 0;
        var rightWidth = RightImage?.PixelSize.Width ?? 0;
        var leftHeight = LeftImage?.PixelSize.Height ?? 0;
        var rightHeight = RightImage?.PixelSize.Height ?? 0;

        StageWidth = Math.Max(920, Math.Max(leftWidth, rightWidth));
        StageHeight = Math.Max(560, Math.Max(leftHeight, rightHeight));
    }

    private void UpdateDifferenceStatus()
    {
        DifferenceStatusText = ImageDifferenceMetric.Compare(LeftImage, RightImage)?.ToStatusText()
            ?? "Load two images to compare";
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnLeftImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        oldValue?.Dispose();
        UpdateStageSize();
        UpdateDifferenceStatus();

        if (!CanUseSplitScreen && IsSplitScreenView)
        {
            SelectSideBySideView();
        }
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnRightImageChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        oldValue?.Dispose();
        UpdateStageSize();
        UpdateDifferenceStatus();

        if (!CanUseSplitScreen && IsSplitScreenView)
        {
            SelectSideBySideView();
        }
    }

    partial void OnZoomScaleChanged(double value)
    {
        ZoomText = $"{Math.Round(value * 100):0}%";
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if ((value is null || !Projects.Contains(value)) && Projects.Count > 0)
        {
            SelectedProject = Projects.FirstOrDefault();
            return;
        }

        if (value is null || SelectedComparison is null || !value.Comparisons.Contains(SelectedComparison))
        {
            SelectedComparison = value?.Comparisons.FirstOrDefault();
        }

        RefreshComparisonRows();
        SyncSelectedProjectRow();
        OnPropertyChanged(nameof(SelectedProjectName));
        OnPropertyChanged(nameof(SelectedComparisonName));
        NotifyWorkspaceStateChanged();

        if (SelectedComparison is null)
        {
            ClearDisplayedComparisonImages();
        }
    }

    partial void OnSelectedComparisonChanged(ComparisonSet? value)
    {
        if (SelectedProject is not null
            && (value is null || !SelectedProject.Comparisons.Contains(value)))
        {
            SelectedComparison = SelectedProject.Comparisons.FirstOrDefault();
        }

        SyncSelectedComparisonRow();
        OnPropertyChanged(nameof(SelectedComparisonName));
        NotifyWorkspaceStateChanged();

        if (value?.ReferenceImage is null)
        {
            LeftImage = null;
            LeftFileName = "Reference image";
        }

        if (value?.CandidateImage is null)
        {
            RightImage = null;
            RightFileName = "Candidate image";
        }
    }

    partial void OnSelectedProjectRowChanged(ProjectListItemViewModel? value)
    {
        if (!ReferenceEquals(SelectedProject, value?.Project))
        {
            SelectedProject = value?.Project;
        }
    }

    partial void OnSelectedComparisonRowChanged(ComparisonListItemViewModel? value)
    {
        if (!ReferenceEquals(SelectedComparison, value?.Comparison))
        {
            SelectedComparison = value?.Comparison;
        }
    }

    private void ClearDisplayedComparisonImages()
    {
        LeftImage = null;
        RightImage = null;
        LeftFileName = "Reference image";
        RightFileName = "Candidate image";
    }
}

public enum ImageSlot
{
    Left,
    Right
}

public enum ComparisonViewMode
{
    SideBySide,
    SplitScreen
}
