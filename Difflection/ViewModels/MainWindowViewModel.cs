using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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
        WorkspaceStatus = new WorkspaceStatusViewModel(Workspace, ComparisonDisplay);
        ImageSet = new ComparisonImageSetViewModel(Workspace, ComparisonDisplay, ProjectStorage);
        ComparisonDisplay.PropertyChanged += OnComparisonDisplayPropertyChanged;
        Workspace.PropertyChanged += OnWorkspacePropertyChanged;
        ImageSet.ImageSetChanged += OnImageSetChanged;
    }

    public MainWindowViewModel(IProjectStorage projectStorage)
    {
        ProjectStorage = projectStorage;
        Workspace = new WorkspaceNavigatorViewModel(projectStorage);
        ToolState = new ComparisonToolStateViewModel(() => HasBothImages);
        WorkspaceStatus = new WorkspaceStatusViewModel(Workspace, ComparisonDisplay);
        ImageSet = new ComparisonImageSetViewModel(Workspace, ComparisonDisplay, projectStorage);
        _monitoredImageVersionCapture = new MonitoredImageVersionCapture(projectStorage);
        ComparisonDisplay.PropertyChanged += OnComparisonDisplayPropertyChanged;
        Workspace.PropertyChanged += OnWorkspacePropertyChanged;
        ImageSet.ImageSetChanged += OnImageSetChanged;
    }

    public ObservableCollection<Project> Projects => Workspace.Projects;

    public ObservableCollection<ProjectListItemViewModel> ProjectRows => Workspace.ProjectRows;

    public ObservableCollection<ComparisonListItemViewModel> SelectedProjectComparisonRows => Workspace.SelectedProjectComparisonRows;

    public ComparisonDisplayViewModel ComparisonDisplay { get; } = new();

    public WorkspaceNavigatorViewModel Workspace { get; }

    public ComparisonToolStateViewModel ToolState { get; }

    public WorkspaceStatusViewModel WorkspaceStatus { get; }

    public ComparisonImageSetViewModel ImageSet { get; }

    public IProjectStorage? ProjectStorage { get; }

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

    public bool HasBothImages => ComparisonDisplay.HasBothImages;

    public bool ShowProjectsEmptyState => WorkspaceStatus.ShowProjectsEmptyState;

    public bool ShowComparisonsEmptyState => WorkspaceStatus.ShowComparisonsEmptyState;

    public string SelectedComparisonImageCountText => Workspace.SelectedComparisonImageCountText;

    public string WorkspaceContextTitle => WorkspaceStatus.WorkspaceContextTitle;

    public string WorkspaceContextDetail => WorkspaceStatus.WorkspaceContextDetail;

    public string WorkspaceActionHint => WorkspaceStatus.WorkspaceActionHint;

    public bool ShowMainEmptyState => WorkspaceStatus.ShowMainEmptyState;

    public string MainEmptyStateTitle => WorkspaceStatus.MainEmptyStateTitle;

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

    public async Task LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        await Workspace.LoadProjectsAsync(cancellationToken);
    }

    public async Task<Project> AddProjectAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        return await Workspace.AddProjectAsync(name, cancellationToken);
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

    public async Task RenameSelectedProjectAsync(string? name, CancellationToken cancellationToken = default)
    {
        await Workspace.RenameSelectedProjectAsync(name, cancellationToken);
    }

    public async Task RenameSelectedComparisonAsync(string? name, CancellationToken cancellationToken = default)
    {
        await Workspace.RenameSelectedComparisonAsync(name, cancellationToken);
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
        Workspace.NotifySelectedComparisonImagesChanged();
        WorkspaceStatus.NotifyImageStateChanged();

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
        Workspace.NotifySelectedComparisonImagesChanged();
        WorkspaceStatus.NotifyImageStateChanged();
        await RefreshCurrentComparisonImagesAsync(cancellationToken);

        return version;
    }

    public async Task RefreshCurrentComparisonImagesAsync(CancellationToken cancellationToken = default)
    {
        await ComparisonDisplay.RefreshCurrentComparisonImagesAsync(SelectedComparison, ProjectStorage, cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.DeleteImageAsync(image, cancellationToken);
    }

    public async Task<bool> LabelImageAsync(ImageAsset image, string? label, CancellationToken cancellationToken = default)
    {
        return await ImageSet.LabelImageAsync(image, label, cancellationToken);
    }

    public async Task<bool> SetReferenceImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.SetReferenceImageAsync(image, cancellationToken);
    }

    public async Task<bool> SetCandidateImageAsync(ImageAsset image, CancellationToken cancellationToken = default)
    {
        return await ImageSet.SetCandidateImageAsync(image, cancellationToken);
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

    private Task SaveProjectAsync(Project project, CancellationToken cancellationToken)
    {
        return ProjectStorage?.SaveProjectAsync(project, cancellationToken) ?? Task.CompletedTask;
    }

    private void OnImageSetChanged(object? sender, EventArgs e)
    {
        WorkspaceStatus.NotifyImageStateChanged();
    }

    private void OnComparisonDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        if (e.PropertyName is not (nameof(ComparisonDisplayViewModel.LeftImage)
            or nameof(ComparisonDisplayViewModel.RightImage)
            or nameof(ComparisonDisplayViewModel.HasBothImages))) return;

        ToolState.NotifyCanUseSplitScreenChanged();
        WorkspaceStatus.NotifyImageStateChanged();
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        if (e.PropertyName is nameof(WorkspaceNavigatorViewModel.SelectedProject)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparison)
            or nameof(WorkspaceNavigatorViewModel.SelectedProjectName)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparisonName)
            or nameof(WorkspaceNavigatorViewModel.SelectedComparisonImageCountText)
            or nameof(WorkspaceNavigatorViewModel.HasProjects)
            or nameof(WorkspaceNavigatorViewModel.ShowProjectsEmptyState)
            or nameof(WorkspaceNavigatorViewModel.ShowComparisonsEmptyState))
        {
            WorkspaceStatus.NotifyWorkspaceStateChanged();
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
