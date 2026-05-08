using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
    private readonly MonitoredImageVersionCapture? _monitoredImageVersionCapture;

    public MainWindowViewModel()
    {
        Workspace = new WorkspaceNavigatorViewModel();
        ToolState = new ComparisonToolStateViewModel(() => HasBothImages);
        ImageSet = new ComparisonImageSetViewModel(Workspace, ComparisonDisplay, ProjectStorage);
        ComparisonDisplay.PropertyChanged += OnComparisonDisplayPropertyChanged;
        Workspace.PropertyChanged += OnWorkspacePropertyChanged;
        ToolState.PropertyChanged += OnToolStatePropertyChanged;
        ImageSet.ImageSetChanged += OnImageSetChanged;
    }

    public MainWindowViewModel(IProjectStorage projectStorage)
    {
        ProjectStorage = projectStorage;
        Workspace = new WorkspaceNavigatorViewModel(projectStorage);
        ToolState = new ComparisonToolStateViewModel(() => HasBothImages);
        ImageSet = new ComparisonImageSetViewModel(Workspace, ComparisonDisplay, projectStorage);
        _monitoredImageVersionCapture = new MonitoredImageVersionCapture(projectStorage);
        ComparisonDisplay.PropertyChanged += OnComparisonDisplayPropertyChanged;
        Workspace.PropertyChanged += OnWorkspacePropertyChanged;
        ToolState.PropertyChanged += OnToolStatePropertyChanged;
        ImageSet.ImageSetChanged += OnImageSetChanged;
    }

    public ObservableCollection<Project> Projects => Workspace.Projects;

    public ObservableCollection<ProjectListItemViewModel> ProjectRows => Workspace.ProjectRows;

    public ObservableCollection<ComparisonListItemViewModel> SelectedProjectComparisonRows => Workspace.SelectedProjectComparisonRows;

    public ComparisonDisplayViewModel ComparisonDisplay { get; } = new();

    public WorkspaceNavigatorViewModel Workspace { get; }

    public ComparisonToolStateViewModel ToolState { get; }

    public ComparisonImageSetViewModel ImageSet { get; }

    public IProjectStorage? ProjectStorage { get; }

    public ComparisonViewMode SelectedViewMode
    {
        get => ToolState.SelectedViewMode;
        set => ToolState.SelectedViewMode = value;
    }

    public Project? SelectedProject
    {
        get => Workspace.SelectedProject;
        set => Workspace.SelectedProject = value;
    }

    public ProjectListItemViewModel? SelectedProjectRow
    {
        get => Workspace.SelectedProjectRow;
        set => Workspace.SelectedProjectRow = value;
    }

    public ComparisonSet? SelectedComparison
    {
        get => Workspace.SelectedComparison;
        set => Workspace.SelectedComparison = value;
    }

    public ComparisonListItemViewModel? SelectedComparisonRow
    {
        get => Workspace.SelectedComparisonRow;
        set => Workspace.SelectedComparisonRow = value;
    }

    public string SplitPercentageText
    {
        get => ToolState.SplitPercentageText;
        set => ToolState.SplitPercentageText = value;
    }

    public double ZoomScale
    {
        get => ToolState.ZoomScale;
        set => ToolState.ZoomScale = value;
    }

    public string ZoomText
    {
        get => ToolState.ZoomText;
        set => ToolState.ZoomText = value;
    }
    public double StageWidth
    {
        get => ComparisonDisplay.StageWidth;
        set => ComparisonDisplay.StageWidth = value;
    }

    public double StageHeight
    {
        get => ComparisonDisplay.StageHeight;
        set => ComparisonDisplay.StageHeight = value;
    }

    public Bitmap? LeftImage
    {
        get => ComparisonDisplay.LeftImage;
        set => ComparisonDisplay.LeftImage = value;
    }

    public Bitmap? RightImage
    {
        get => ComparisonDisplay.RightImage;
        set => ComparisonDisplay.RightImage = value;
    }

    public string LeftFileName
    {
        get => ComparisonDisplay.LeftFileName;
        set => ComparisonDisplay.LeftFileName = value;
    }

    public string RightFileName
    {
        get => ComparisonDisplay.RightFileName;
        set => ComparisonDisplay.RightFileName = value;
    }

    public string DifferenceStatusText
    {
        get => ComparisonDisplay.DifferenceStatusText;
        set => ComparisonDisplay.DifferenceStatusText = value;
    }

    public bool HasLeftImage => ComparisonDisplay.HasLeftImage;

    public bool HasRightImage => ComparisonDisplay.HasRightImage;

    public bool HasAnyImage => ComparisonDisplay.HasAnyImage;

    public bool HasBothImages => ComparisonDisplay.HasBothImages;

    public bool HasProjects => Workspace.HasProjects;

    public bool ShowProjectsEmptyState => Workspace.ShowProjectsEmptyState;

    public bool ShowComparisonsEmptyState => Workspace.ShowComparisonsEmptyState;

    public bool CanDeleteSelectedProject => Workspace.CanDeleteSelectedProject;

    public bool CanAddComparison => Workspace.CanAddComparison;

    public bool CanDeleteSelectedComparison => Workspace.CanDeleteSelectedComparison;

    public string SelectedProjectName
    {
        get => Workspace.SelectedProjectName;
        set => Workspace.SelectedProjectName = value;
    }

    public string SelectedComparisonName
    {
        get => Workspace.SelectedComparisonName;
        set => Workspace.SelectedComparisonName = value;
    }

    public IReadOnlyList<ComparisonSet> SelectedProjectComparisons => Workspace.SelectedProjectComparisons;

    public IReadOnlyList<ImageAsset> SelectedComparisonImages => Workspace.SelectedComparisonImages;

    public void BeginRenameProject(Project project)
    {
        Workspace.BeginRenameProject(project);
    }

    [RelayCommand]
    public void BeginRenameProject(ProjectListItemViewModel projectRow)
    {
        Workspace.BeginRenameProject(projectRow);
    }

    public void BeginRenameComparison(ComparisonSet comparison)
    {
        Workspace.BeginRenameComparison(comparison);
    }

    [RelayCommand]
    public void BeginRenameComparison(ComparisonListItemViewModel comparisonRow)
    {
        Workspace.BeginRenameComparison(comparisonRow);
    }

    public async Task CommitProjectRenameAsync(ProjectListItemViewModel row, CancellationToken cancellationToken = default)
    {
        await Workspace.CommitProjectRenameAsync(row, cancellationToken);
    }

    public async Task CommitComparisonRenameAsync(ComparisonListItemViewModel row, CancellationToken cancellationToken = default)
    {
        await Workspace.CommitComparisonRenameAsync(row, cancellationToken);
    }

    public void CancelProjectRename(ProjectListItemViewModel row)
    {
        WorkspaceNavigatorViewModel.CancelProjectRename(row);
    }

    public void CancelComparisonRename(ComparisonListItemViewModel row)
    {
        WorkspaceNavigatorViewModel.CancelComparisonRename(row);
    }

    public async Task CommitActiveInlineRenamesAsync(CancellationToken cancellationToken = default)
    {
        await Workspace.CommitActiveInlineRenamesAsync(cancellationToken);
    }

    public string SelectedComparisonImageCountText => Workspace.SelectedComparisonImageCountText;

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
        return ImageSet.CanSetReferenceImage(image);
    }

    public bool CanSetCandidateImage(ImageAsset? image)
    {
        return ImageSet.CanSetCandidateImage(image);
    }

    public double LeftImageWidth => ComparisonDisplay.LeftImageWidth;

    public double LeftImageHeight => ComparisonDisplay.LeftImageHeight;

    public double RightImageWidth => ComparisonDisplay.RightImageWidth;

    public double RightImageHeight => ComparisonDisplay.RightImageHeight;

    public bool IsSideBySideView => ToolState.IsSideBySideView;

    public bool IsSplitScreenView => ToolState.IsSplitScreenView;

    public bool CanUseSplitScreen => ToolState.CanUseSplitScreen;

    public string CurrentViewTitle => ToolState.CurrentViewTitle;

    public double SideBySideStageWidth => ComparisonDisplay.SideBySideStageWidth;

    public double SideBySideStageHeight => ComparisonDisplay.SideBySideStageHeight;

    public async Task LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        await Workspace.LoadProjectsAsync(cancellationToken);
    }

    public async Task<Project> AddProjectAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        return await Workspace.AddProjectAsync(name, cancellationToken);
    }

    [RelayCommand]
    public async Task<Project> AddProjectForInlineRenameAsync(CancellationToken cancellationToken = default)
    {
        return await Workspace.AddProjectForInlineRenameAsync(cancellationToken);
    }

    public async Task<bool> DeleteProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        return await Workspace.DeleteProjectAsync(project, cancellationToken);
    }

    [RelayCommand]
    public Task<bool> DeleteSelectedProjectAsync(CancellationToken cancellationToken = default)
    {
        return Workspace.DeleteSelectedProjectAsync(cancellationToken);
    }

    public async Task<ComparisonSet> AddComparisonAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        return await Workspace.AddComparisonAsync(name, cancellationToken);
    }

    [RelayCommand]
    public async Task<ComparisonSet> AddComparisonForInlineRenameAsync(CancellationToken cancellationToken = default)
    {
        return await Workspace.AddComparisonForInlineRenameAsync(cancellationToken);
    }

    public async Task EnsureProjectAndComparisonAsync(CancellationToken cancellationToken = default)
    {
        await Workspace.EnsureProjectAndComparisonAsync(cancellationToken);
    }

    public async Task RenameSelectedProjectAsync(string? name, CancellationToken cancellationToken = default)
    {
        await Workspace.RenameSelectedProjectAsync(name, cancellationToken);
    }

    public async Task RenameSelectedComparisonAsync(string? name, CancellationToken cancellationToken = default)
    {
        await Workspace.RenameSelectedComparisonAsync(name, cancellationToken);
    }

    public async Task RenameProjectAsync(Project project, string? name, CancellationToken cancellationToken = default)
    {
        await Workspace.RenameProjectAsync(project, name, cancellationToken);
    }

    public async Task RenameComparisonAsync(ComparisonSet comparison, string? name, CancellationToken cancellationToken = default)
    {
        await Workspace.RenameComparisonAsync(comparison, name, cancellationToken);
    }

    public async Task<bool> DeleteComparisonAsync(ComparisonSet comparison, CancellationToken cancellationToken = default)
    {
        return await Workspace.DeleteComparisonAsync(comparison, cancellationToken);
    }

    [RelayCommand]
    public Task<bool> DeleteSelectedComparisonAsync(CancellationToken cancellationToken = default)
    {
        return Workspace.DeleteSelectedComparisonAsync(cancellationToken);
    }

    public async Task<ImageAsset> AddImageAsync(
        string sourceName,
        Stream content,
        string? mediaType = null,
        string? label = null,
        ImageSourceMetadata? originalFileMetadata = null,
        CancellationToken cancellationToken = default)
    {
        return await ImageSet.AddImageAsync(sourceName, content, mediaType, label, originalFileMetadata, cancellationToken);
    }

    public async Task<ImageAsset> AddImageAsync(
        IStorageFile file,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        return await ImageSet.AddImageAsync(file, label, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageAsset>> AddFilesToCurrentComparisonAsync(
        IEnumerable<IStorageFile> files,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        return await ImageSet.AddFilesToCurrentComparisonAsync(files, maxFiles, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageAsset>> AddFilesToCurrentComparisonAfterCommittingRenamesAsync(
        IEnumerable<IStorageFile> files,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        return await ImageSet.AddFilesToCurrentComparisonAfterCommittingRenamesAsync(files, maxFiles, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageAsset>> AddBrowserFilesToCurrentComparisonAsync(
        IReadOnlyList<string> fileNames,
        IReadOnlyList<byte[]> fileContents,
        int? maxFiles = null,
        CancellationToken cancellationToken = default)
    {
        return await ImageSet.AddBrowserFilesToCurrentComparisonAsync(fileNames, fileContents, maxFiles, cancellationToken);
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
        return await ComparisonDisplay.LoadImageAssetAsync(slot, image, ProjectStorage, cancellationToken);
    }

    public async Task RefreshCurrentComparisonImagesAsync(CancellationToken cancellationToken = default)
    {
        await ComparisonDisplay.RefreshCurrentComparisonImagesAsync(SelectedComparison, ProjectStorage, cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.DeleteImageAsync(image, cancellationToken);
    }

    [RelayCommand]
    public async Task<bool> DeleteImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.DeleteImageAndRefreshAsync(image, cancellationToken);
    }

    public async Task<bool> LabelImageAsync(ImageAsset image, string? label, CancellationToken cancellationToken = default)
    {
        return await ImageSet.LabelImageAsync(image, label, cancellationToken);
    }

    public async Task<bool> SetReferenceImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.SetReferenceImageAsync(image, cancellationToken);
    }

    [RelayCommand]
    public async Task<bool> SetReferenceImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.SetReferenceImageAndRefreshAsync(image, cancellationToken);
    }

    public async Task<bool> SetCandidateImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.SetCandidateImageAsync(image, cancellationToken);
    }

    [RelayCommand]
    public async Task<bool> SetCandidateImageAndRefreshAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.SetCandidateImageAndRefreshAsync(image, cancellationToken);
    }

    public void SetZoomScale(double zoomScale)
    {
        ToolState.SetZoomScale(zoomScale);
    }

    public bool TrySetZoomText(string? text)
    {
        return ToolState.TrySetZoomText(text);
    }

    public void SelectSideBySideView()
    {
        ToolState.SelectSideBySideView();
    }

    public void SelectSplitScreenView()
    {
        ToolState.SelectSplitScreenView();
    }

    public async Task LoadImageAsync(ImageSlot slot, IStorageFile file)
    {
        await ComparisonDisplay.LoadImageAsync(slot, file);
    }

    public async Task LoadImageAsync(ImageSlot slot, string filePath)
    {
        await ComparisonDisplay.LoadImageAsync(slot, filePath);
    }

    public async Task LoadImageAsync(ImageSlot slot, string fileName, Stream stream)
    {
        await ComparisonDisplay.LoadImageAsync(slot, fileName, stream);
    }

    public void DisposeImages()
    {
        ComparisonDisplay.DisposeImages();
    }

    private Task SaveProjectAsync(Project project, CancellationToken cancellationToken)
    {
        return ProjectStorage?.SaveProjectAsync(project, cancellationToken) ?? Task.CompletedTask;
    }

    private void NotifySelectedComparisonImagesChanged()
    {
        Workspace.NotifySelectedComparisonImagesChanged();
        NotifySelectedComparisonImagePropertiesChanged();
    }

    private void NotifySelectedComparisonImagePropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedComparisonImages));
        OnPropertyChanged(nameof(SelectedComparisonImageCountText));
        OnPropertyChanged(nameof(WorkspaceContextDetail));
        OnPropertyChanged(nameof(WorkspaceActionHint));
        OnPropertyChanged(nameof(ShowWorkspaceActionHint));
        OnPropertyChanged(nameof(ShowMainEmptyState));
        OnPropertyChanged(nameof(MainEmptyStateTitle));
        OnPropertyChanged(nameof(MainEmptyStateMessage));
    }

    private void OnImageSetChanged(object? sender, EventArgs e)
    {
        NotifySelectedComparisonImagePropertiesChanged();
    }

    private void NotifyWorkspaceStateChanged()
    {
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(ShowProjectsEmptyState));
        OnPropertyChanged(nameof(ShowComparisonsEmptyState));
        OnPropertyChanged(nameof(WorkspaceContextTitle));
        OnPropertyChanged(nameof(WorkspaceContextDetail));
        OnPropertyChanged(nameof(WorkspaceActionHint));
        OnPropertyChanged(nameof(ShowWorkspaceActionHint));
        OnPropertyChanged(nameof(ShowMainEmptyState));
        OnPropertyChanged(nameof(MainEmptyStateTitle));
        OnPropertyChanged(nameof(MainEmptyStateMessage));
    }

    private void OnComparisonDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);

        if (e.PropertyName is not (nameof(ComparisonDisplayViewModel.LeftImage)
            or nameof(ComparisonDisplayViewModel.RightImage)
            or nameof(ComparisonDisplayViewModel.HasBothImages))) return;

        ToolState.NotifyCanUseSplitScreenChanged();
        OnPropertyChanged(nameof(ShowMainEmptyState));
    }

    private void OnToolStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);

        if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedProject)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparison)
            or nameof(WorkspaceNavigatorViewModel.SelectedProjectName)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparisonName)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparisonImageCountText)
            or nameof(WorkspaceNavigatorViewModel.HasProjects)
            or nameof(WorkspaceNavigatorViewModel.ShowProjectsEmptyState)
            or nameof(WorkspaceNavigatorViewModel.ShowComparisonsEmptyState))
        {
            NotifyWorkspaceStateChanged();
        }

        switch (e.PropertyName)
        {
            case nameof(WorkspaceNavigatorViewModel.SelectedProject)
                when SelectedComparison is null:
                ClearDisplayedComparisonImages();
                break;
            case nameof(WorkspaceNavigatorViewModel.SelectedComparison):
            {
                if (SelectedComparison?.ReferenceImage is null)
                {
                    LeftImage = null;
                    LeftFileName = "Reference image";
                }

                if (SelectedComparison?.CandidateImage is null)
                {
                    RightImage = null;
                    RightFileName = "Candidate image";
                }

                break;
            }
        }
    }

    private void ClearDisplayedComparisonImages()
    {
        ComparisonDisplay.Clear();
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
