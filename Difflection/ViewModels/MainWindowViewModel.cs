using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Difflection.Models;
using Difflection.Monitoring;
using Difflection.Storage;

namespace Difflection.ViewModels;

public class MainWindowViewModel : ViewModelBase
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

    public ComparisonDisplayViewModel ComparisonDisplay { get; } = new();

    public WorkspaceNavigatorViewModel Workspace { get; }

    public ComparisonToolStateViewModel ToolState { get; }

    public WorkspaceStatusViewModel WorkspaceStatus { get; }

    public ComparisonImageSetViewModel ImageSet { get; }

    public IProjectStorage? ProjectStorage { get; }

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

    public async Task LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        await Workspace.LoadProjectsAsync(cancellationToken);
    }

    public async Task<bool> SetImageMonitoringAsync(
        ImageAsset image,
        ImageMonitoringRole monitoringRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (Workspace.SelectedProject is null || Workspace.SelectedComparison is null || !Workspace.SelectedComparison.Images.Contains(image))
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
        Workspace.SelectedComparison.UpdatedAt = DateTimeOffset.UtcNow;
        Workspace.SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        Workspace.NotifySelectedComparisonImagesChanged();
        WorkspaceStatus.NotifyImageStateChanged();

        await SaveProjectAsync(Workspace.SelectedProject, cancellationToken);
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

        if (version is null || !ReferenceEquals(project, Workspace.SelectedProject) ||
            !ReferenceEquals(comparison, Workspace.SelectedComparison)) return version;
        Workspace.NotifySelectedComparisonImagesChanged();
        WorkspaceStatus.NotifyImageStateChanged();
        await ComparisonDisplay.RefreshCurrentComparisonImagesAsync(Workspace.SelectedComparison, ProjectStorage, cancellationToken);

        return version;
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
                when Workspace.SelectedComparison is null:
                ClearDisplayedComparisonImages();
                break;
            case nameof(WorkspaceNavigatorViewModel.SelectedComparison):
            {
                if (Workspace.SelectedComparison?.ReferenceImage is null)
                {
                    LeftImage = null;
                    LeftFileName = "Reference image";
                }

                if (Workspace.SelectedComparison?.CandidateImage is null)
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
