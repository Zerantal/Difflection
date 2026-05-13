using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private set => ComparisonDisplay.LeftImage = value;
    }

    public Bitmap? RightImage
    {
        get => ComparisonDisplay.RightImage;
        private set => ComparisonDisplay.RightImage = value;
    }

    public string LeftFileName
    {
        get => ComparisonDisplay.LeftFileName;
        private set => ComparisonDisplay.LeftFileName = value;
    }

    public string RightFileName
    {
        get => ComparisonDisplay.RightFileName;
        private set => ComparisonDisplay.RightFileName = value;
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

        if (ProjectStorage is not null)
        {
            await ComparisonDisplay.RefreshCurrentComparisonImagesAsync(Workspace.SelectedComparison, ProjectStorage, cancellationToken);
        }
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

        var capturedRole = changedImage.MonitoringRole;
        var version = await _monitoredImageVersionCapture.CaptureAsync(project, comparison, changedImage, cancellationToken);

        if (version is null)
        {
            return null;
        }

        if (capturedRole == ImageMonitoringRole.Candidate)
        {
            await UpdateComparisonReviewStateAsync(project, comparison, cancellationToken);
        }

        if (!ReferenceEquals(project, Workspace.SelectedProject) ||
            !ReferenceEquals(comparison, Workspace.SelectedComparison)) return version;
        Workspace.NotifyComparisonImagesChanged(comparison);
        WorkspaceStatus.NotifyImageStateChanged();
        await ComparisonDisplay.RefreshCurrentComparisonImagesAsync(Workspace.SelectedComparison, ProjectStorage, cancellationToken);

        return version;
    }

    public async Task<int> RefreshProjectSourceImagesAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (_monitoredImageVersionCapture is null || !Workspace.Projects.Contains(project))
        {
            return 0;
        }

        var capturedCount = 0;
        foreach (var comparison in project.Comparisons.ToArray())
        {
            capturedCount += await RefreshComparisonSourceImagesAsync(project, comparison, cancellationToken);
        }

        return capturedCount;
    }

    public Task<int> RefreshComparisonSourceImagesAsync(
        ComparisonSet comparison,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        if (Workspace.SelectedProject is null || !Workspace.SelectedProject.Comparisons.Contains(comparison))
        {
            return Task.FromResult(0);
        }

        return RefreshComparisonSourceImagesAsync(Workspace.SelectedProject, comparison, cancellationToken);
    }

    private async Task<int> RefreshComparisonSourceImagesAsync(
        Project project,
        ComparisonSet comparison,
        CancellationToken cancellationToken)
    {
        if (_monitoredImageVersionCapture is null)
        {
            return 0;
        }

        var roleImages = GetCurrentRoleImages(comparison);
        var capturedCount = 0;
        var capturedCandidate = false;

        foreach (var (image, role) in roleImages)
        {
            var version = await _monitoredImageVersionCapture.RefreshCurrentRoleImageAsync(
                project,
                comparison,
                image,
                role,
                cancellationToken);

            if (version is not null)
            {
                capturedCount++;
                capturedCandidate |= role == ImageMonitoringRole.Candidate;
            }
        }

        if (capturedCount == 0)
        {
            return 0;
        }

        if (capturedCandidate)
        {
            await UpdateComparisonReviewStateAsync(project, comparison, cancellationToken);
        }
        Workspace.NotifyComparisonImagesChanged(comparison);
        WorkspaceStatus.NotifyImageStateChanged();

        if (ReferenceEquals(project, Workspace.SelectedProject) && ReferenceEquals(comparison, Workspace.SelectedComparison))
        {
            await ImageSet.RefreshImageRowsAsync(cancellationToken);
            await ComparisonDisplay.RefreshCurrentComparisonImagesAsync(Workspace.SelectedComparison, ProjectStorage, cancellationToken);
        }

        return capturedCount;
    }

    private async Task UpdateComparisonReviewStateAsync(
        Project project,
        ComparisonSet comparison,
        CancellationToken cancellationToken)
    {
        var requiresReview = await CurrentRoleImagesDifferAsync(comparison, cancellationToken);
        if (comparison.RequiresReview == requiresReview)
        {
            return;
        }

        comparison.RequiresReview = requiresReview;
        comparison.UpdatedAt = DateTimeOffset.UtcNow;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveProjectAsync(project, cancellationToken);
    }

    private async Task<bool> CurrentRoleImagesDifferAsync(
        ComparisonSet comparison,
        CancellationToken cancellationToken)
    {
        if (ProjectStorage is null || comparison.ReferenceImage is not { } reference || comparison.CandidateImage is not { } candidate)
        {
            return false;
        }

        await using var referenceStream = await ProjectStorage.LoadImageAsync(reference, cancellationToken);
        await using var candidateStream = await ProjectStorage.LoadImageAsync(candidate, cancellationToken);
        using var referenceBitmap = new Bitmap(referenceStream);
        using var candidateBitmap = new Bitmap(candidateStream);
        return ImageDifferenceMetric.Compare(referenceBitmap, candidateBitmap)?.DifferentPixels > 0;
    }

    private static IReadOnlyList<(ImageAsset Image, ImageMonitoringRole Role)> GetCurrentRoleImages(ComparisonSet comparison)
    {
        List<(ImageAsset Image, ImageMonitoringRole Role)> roleImages = [];

        if (comparison.ReferenceImage is { } reference)
        {
            roleImages.Add((reference, ImageMonitoringRole.Reference));
        }

        if (comparison.CandidateImage is { } candidate && roleImages.All(item => item.Image.Id != candidate.Id))
        {
            roleImages.Add((candidate, ImageMonitoringRole.Candidate));
        }

        return roleImages;
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

        switch (e.PropertyName)
        {
            case nameof(ComparisonDisplayViewModel.LeftImage):
            case nameof(ComparisonDisplayViewModel.RightImage):
            case nameof(ComparisonDisplayViewModel.HasBothImages):
                ToolState.NotifyCanUseSplitScreenChanged();
                WorkspaceStatus.NotifyImageStateChanged();
                break;
            case nameof(ComparisonDisplayViewModel.DifferenceStatusText):
                OnPropertyChanged(nameof(DifferenceStatusText));
                break;
            case nameof(ComparisonDisplayViewModel.LeftFileName):
                OnPropertyChanged(nameof(LeftFileName));
                break;
            case nameof(ComparisonDisplayViewModel.RightFileName):
                OnPropertyChanged(nameof(RightFileName));
                break;
        }
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
                    LeftFileName = "Baseline image";
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
