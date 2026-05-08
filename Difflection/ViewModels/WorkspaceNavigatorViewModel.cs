using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Difflection.Models;
using Difflection.Storage;

namespace Difflection.ViewModels;

public partial class WorkspaceNavigatorViewModel : ViewModelBase
{
    private const string DefaultProjectName = "Untitled Project";
    private const string DefaultComparisonName = "Untitled Comparison";

    public WorkspaceNavigatorViewModel()
    {
    }

    public WorkspaceNavigatorViewModel(IProjectStorage projectStorage)
    {
        ProjectStorage = projectStorage;
    }

    public ObservableCollection<Project> Projects { get; } = [];

    public ObservableCollection<ProjectListItemViewModel> ProjectRows { get; } = [];

    public ObservableCollection<ComparisonListItemViewModel> SelectedProjectComparisonRows { get; } = [];

    private IProjectStorage? ProjectStorage { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedProject))]
    [NotifyPropertyChangedFor(nameof(SelectedProjectComparisons))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedProject))]
    [NotifyPropertyChangedFor(nameof(CanAddComparison))]
    [NotifyPropertyChangedFor(nameof(SelectedProjectName))]
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
    [NotifyPropertyChangedFor(nameof(ShowComparisonsEmptyState))]
    public partial ComparisonSet? SelectedComparison { get; set; }

    [ObservableProperty]
    public partial ComparisonListItemViewModel? SelectedComparisonRow { get; set; }

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
        }
    }

    public IReadOnlyList<ComparisonSet> SelectedProjectComparisons => SelectedProject?.Comparisons.ToArray() ?? [];

    public IReadOnlyList<ImageAsset> SelectedComparisonImages => SelectedComparison?.Images.ToArray() ?? [];

    public string SelectedComparisonImageCountText
    {
        get
        {
            var imageCount = SelectedComparison?.Images.Count ?? 0;
            return imageCount == 1 ? "1 image" : $"{imageCount} images";
        }
    }

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

    public static void CancelProjectRename(ProjectListItemViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.CancelEdit();
    }

    public static void CancelComparisonRename(ComparisonListItemViewModel row)
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

    private void RefreshComparison(ComparisonSet comparison)
    {
        FindComparisonRow(comparison)?.Refresh();
    }

    public void NotifySelectedComparisonImagesChanged()
    {
        if (SelectedComparison is not null)
        {
            RefreshComparison(SelectedComparison);
        }

        OnPropertyChanged(nameof(SelectedComparisonImages));
        OnPropertyChanged(nameof(SelectedComparisonImageCountText));
    }

    public void RenameDefaultComparisonFromFirstImage(ComparisonSet comparison, string imageLabel)
    {
        if (comparison.Images.Count > 0 || !string.Equals(comparison.Name, DefaultComparisonName, StringComparison.Ordinal))
        {
            return;
        }

        comparison.Name = NormalizeName(imageLabel, DefaultComparisonName);
        RefreshComparison(comparison);
        OnPropertyChanged(nameof(SelectedComparisonName));
        OnPropertyChanged(nameof(SelectedProjectComparisons));
    }

    private static string NormalizeName(string? name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    private Task SaveProjectAsync(Project project, CancellationToken cancellationToken)
    {
        return ProjectStorage?.SaveProjectAsync(project, cancellationToken) ?? Task.CompletedTask;
    }

    private void NotifyWorkspaceStateChanged()
    {
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(ShowProjectsEmptyState));
        OnPropertyChanged(nameof(ShowComparisonsEmptyState));
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
}
