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
using JetBrains.Annotations;

namespace Difflection.ViewModels;

public partial class WorkspaceNavigatorViewModel : ViewModelBase
{
    private const string DefaultProjectName = "Untitled Project";
    private const string DefaultComparisonName = "Untitled Comparison";
    private readonly ObservableCollection<Project> _projects = [];
    private readonly Dictionary<Guid, ProjectListItemViewModel> _projectRowsById = [];
    private readonly Dictionary<Guid, ComparisonListItemViewModel> _comparisonRowsById = [];

    public WorkspaceNavigatorViewModel()
    {
    }

    public WorkspaceNavigatorViewModel(IProjectStorage projectStorage)
    {
        ProjectStorage = projectStorage;
    }

    public IReadOnlyList<Project> Projects => _projects;

    public ObservableCollection<ProjectListItemViewModel> ProjectRows { get; } = [];

    public ObservableCollection<ComparisonListItemViewModel> SelectedProjectComparisonRows { get; } = [];

    private IProjectStorage? ProjectStorage { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedProject))]
    [NotifyPropertyChangedFor(nameof(HasSelectedProject))]
    [NotifyPropertyChangedFor(nameof(SelectedProjectComparisons))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedProject))]
    [NotifyPropertyChangedFor(nameof(CanAddComparison))]
    [NotifyPropertyChangedFor(nameof(SelectedProjectName))]
    [NotifyPropertyChangedFor(nameof(ShowComparisonsEmptyState))]
    public partial ProjectListItemViewModel? SelectedProjectRow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedComparison))]
    [NotifyPropertyChangedFor(nameof(HasSelectedComparison))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedComparison))]
    [NotifyPropertyChangedFor(nameof(SelectedComparisonImages))]
    [NotifyPropertyChangedFor(nameof(SelectedComparisonName))]
    [NotifyPropertyChangedFor(nameof(SelectedComparisonImageCountText))]
    public partial ComparisonListItemViewModel? SelectedComparisonRow { get; set; }

    public Project? SelectedProject
    {
        get => SelectedProjectRow?.Project;
        set => SelectProject(value);
    }

    public ComparisonSet? SelectedComparison
    {
        get => SelectedComparisonRow?.Comparison;
        set => SelectComparison(value);
    }

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
            SelectedProjectRow?.Refresh();
            NotifySelectedProjectMutated();
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
            SelectedComparisonRow?.Refresh();
            NotifySelectedComparisonMutated();
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

    [RelayCommand]
    private void BeginRenameProject(ProjectListItemViewModel projectRow)
    {
        ArgumentNullException.ThrowIfNull(projectRow);

        SelectedProjectRow?.CancelEdit();
        SelectedProjectRow = projectRow;
        projectRow.BeginEdit();
    }

    [RelayCommand]
    private void BeginRenameComparison(ComparisonListItemViewModel comparisonRow)
    {
        ArgumentNullException.ThrowIfNull(comparisonRow);

        SelectedComparisonRow?.CancelEdit();
        SelectedComparisonRow = comparisonRow;
        comparisonRow.BeginEdit();
    }

    [RelayCommand]
    private async Task CommitProjectRenameAsync(ProjectListItemViewModel row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsEditing)
        {
            return;
        }

        await RenameProjectAsync(row, row.DraftName, cancellationToken);
        row.EndEdit();
    }

    [RelayCommand]
    private async Task CommitComparisonRenameAsync(ComparisonListItemViewModel row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsEditing)
        {
            return;
        }

        await RenameComparisonAsync(row, row.DraftName, cancellationToken);
        row.EndEdit();
    }

    public static void CancelProjectRename(ProjectListItemViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.CancelEdit();
    }

    [RelayCommand]
    public void CancelProjectRenameEdit(ProjectListItemViewModel row)
    {
        CancelProjectRename(row);
    }

    private static void CancelComparisonRename(ComparisonListItemViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.CancelEdit();
    }

    [RelayCommand]
    public void CancelComparisonRenameEdit(ComparisonListItemViewModel row)
    {
        CancelComparisonRename(row);
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

        _projects.Clear();

        foreach (var project in projects)
        {
            _projects.Add(project);
        }

        RefreshProjectRows();
        SelectedProjectRow = ProjectRows.FirstOrDefault();
        NotifyProjectCollectionMutated();
    }

    public async Task<Project> AddProjectAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            Name = NormalizeName(name, DefaultProjectName)
        };

        _projects.Add(project);
        RefreshProjectRows();
        SelectedProjectRow = _projectRowsById[project.Id];

        await SaveProjectAsync(project, cancellationToken);
        return project;
    }

    [RelayCommand]
    [UsedImplicitly]
    public async Task<Project> AddProjectForInlineRenameAsync(CancellationToken cancellationToken = default)
    {
        var project = await AddProjectAsync(cancellationToken: cancellationToken);
        if (SelectedProjectRow is { Project: var selectedProject } row && ReferenceEquals(selectedProject, project))
        {
            BeginRenameProject(row);
        }

        return project;
    }

    public async Task<bool> DeleteProjectAsync(ProjectListItemViewModel projectRow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectRow);

        var project = projectRow.Project;
        var wasSelected = ReferenceEquals(SelectedProjectRow, projectRow);
        var removed = _projects.Remove(project);

        if (!removed)
        {
            return false;
        }

        RefreshProjectRows();
        if (wasSelected)
        {
            SelectedProjectRow = ProjectRows.FirstOrDefault();
        }

        if (ProjectStorage is not null)
        {
            await ProjectStorage.DeleteProjectAsync(project.Id, cancellationToken);
        }

        return true;
    }

    [RelayCommand]
    public Task<bool> DeleteSelectedProjectAsync(CancellationToken cancellationToken = default)
    {
        return SelectedProjectRow is null
            ? Task.FromResult(false)
            : DeleteProjectAsync(SelectedProjectRow, cancellationToken);
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
        SelectedProjectRow?.Refresh();
        NotifySelectedProjectMutated();
        SelectedComparisonRow = _comparisonRowsById[comparison.Id];

        await SaveProjectAsync(SelectedProject, cancellationToken);
        return comparison;
    }

    [RelayCommand]
    public async Task<ComparisonSet> AddComparisonForInlineRenameAsync(CancellationToken cancellationToken = default)
    {
        var comparison = await AddComparisonAsync(cancellationToken: cancellationToken);
        if (SelectedComparisonRow is { Comparison: var selectedComparison } row && ReferenceEquals(selectedComparison, comparison))
        {
            BeginRenameComparison(row);
        }

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
        if (SelectedProjectRow is null)
        {
            return;
        }

        await RenameProjectAsync(SelectedProjectRow, name, cancellationToken);
    }

    public async Task RenameSelectedComparisonAsync(string? name, CancellationToken cancellationToken = default)
    {
        if (SelectedProject is null || SelectedComparisonRow is null)
        {
            return;
        }

        await RenameComparisonAsync(SelectedComparisonRow, name, cancellationToken);
    }

    private async Task RenameProjectAsync(ProjectListItemViewModel projectRow, string? name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectRow);

        var project = projectRow.Project;
        if (!_projects.Contains(project))
        {
            return;
        }

        project.Name = NormalizeName(name, DefaultProjectName);
        project.UpdatedAt = DateTimeOffset.UtcNow;
        projectRow.Refresh();
        if (ReferenceEquals(projectRow, SelectedProjectRow))
        {
            NotifySelectedProjectMutated();
        }

        await SaveProjectAsync(project, cancellationToken);
    }

    private async Task RenameComparisonAsync(ComparisonListItemViewModel comparisonRow, string? name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comparisonRow);

        var comparison = comparisonRow.Comparison;
        if (SelectedProject is null || !SelectedProject.Comparisons.Contains(comparison))
        {
            return;
        }

        comparison.Name = NormalizeName(name, DefaultComparisonName);
        comparison.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveProjectAsync(SelectedProject, cancellationToken);
        comparisonRow.Refresh();
        if (ReferenceEquals(comparisonRow, SelectedComparisonRow))
        {
            NotifySelectedComparisonMutated();
        }
    }

    public async Task<bool> DeleteComparisonAsync(ComparisonListItemViewModel comparisonRow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comparisonRow);

        if (SelectedProject is null)
        {
            return false;
        }

        var comparison = comparisonRow.Comparison;
        var removed = SelectedProject.Comparisons.Remove(comparison);

        if (!removed)
        {
            return false;
        }

        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        RefreshComparisonRows();
        SelectedProjectRow?.Refresh();

        NotifySelectedProjectMutated();
        await SaveProjectAsync(SelectedProject, cancellationToken);
        return true;
    }

    [RelayCommand]
    public Task<bool> DeleteSelectedComparisonAsync(CancellationToken cancellationToken = default)
    {
        return SelectedComparisonRow is null
            ? Task.FromResult(false)
            : DeleteComparisonAsync(SelectedComparisonRow, cancellationToken);
    }

    public void NotifySelectedComparisonImagesChanged()
    {
        if (SelectedComparison is not null)
        {
            NotifyComparisonImagesChanged(SelectedComparison);
        }
    }

    public void NotifyComparisonImagesChanged(ComparisonSet comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        if (_comparisonRowsById.TryGetValue(comparison.Id, out var comparisonRow))
        {
            comparisonRow.Refresh();
        }

        if (ReferenceEquals(comparison, SelectedComparison))
        {
            OnPropertyChanged(nameof(SelectedComparisonImages));
            OnPropertyChanged(nameof(SelectedComparisonImageCountText));
        }
    }

    public void RenameDefaultComparisonFromFirstImage(ComparisonSet comparison, string imageLabel)
    {
        if (comparison.Images.Count > 0 || !string.Equals(comparison.Name, DefaultComparisonName, StringComparison.Ordinal))
        {
            return;
        }

        comparison.Name = NormalizeName(imageLabel, DefaultComparisonName);
        if (_comparisonRowsById.TryGetValue(comparison.Id, out var comparisonRow))
        {
            comparisonRow.Refresh();
        }

        if (ReferenceEquals(comparison, SelectedComparison))
        {
            NotifySelectedComparisonMutated();
        }
    }

    private static string NormalizeName(string? name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    private Task SaveProjectAsync(Project project, CancellationToken cancellationToken)
    {
        return ProjectStorage?.SaveProjectAsync(project, cancellationToken) ?? Task.CompletedTask;
    }

    private void NotifyProjectCollectionMutated()
    {
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(ShowProjectsEmptyState));
        OnPropertyChanged(nameof(ShowComparisonsEmptyState));
    }

    private void NotifySelectedProjectMutated()
    {
        OnPropertyChanged(nameof(SelectedProjectComparisons));
        OnPropertyChanged(nameof(SelectedProjectName));
        OnPropertyChanged(nameof(ShowComparisonsEmptyState));
    }

    private void NotifySelectedComparisonMutated()
    {
        OnPropertyChanged(nameof(SelectedComparisonName));
        OnPropertyChanged(nameof(SelectedComparisonImageCountText));
        OnPropertyChanged(nameof(SelectedComparisonImages));
    }

    private void NotifyComparisonRowsMutated()
    {
        OnPropertyChanged(nameof(SelectedProjectComparisonRows));
        OnPropertyChanged(nameof(ShowComparisonsEmptyState));
    }

    private void RefreshProjectRows()
    {
        var selectedProject = SelectedProject;
        SynchronizeProjectRows();

        SelectedProjectRow = selectedProject is null
            ? null
            : _projectRowsById.GetValueOrDefault(selectedProject.Id);
        OnPropertyChanged(nameof(ProjectRows));
        NotifyProjectCollectionMutated();
    }

    private void RefreshComparisonRows()
    {
        var selectedComparison = SelectedComparison;
        SynchronizeComparisonRows();

        SelectedComparisonRow = selectedComparison is null
            ? SelectedProjectComparisonRows.FirstOrDefault()
            : _comparisonRowsById.GetValueOrDefault(selectedComparison.Id) ?? SelectedProjectComparisonRows.FirstOrDefault();
        NotifyComparisonRowsMutated();
    }

    private void SynchronizeProjectRows()
    {
        for (var index = ProjectRows.Count - 1; index >= 0; index--)
        {
            var row = ProjectRows[index];
            if (!_projects.Contains(row.Project))
            {
                ProjectRows.RemoveAt(index);
                _projectRowsById.Remove(row.Id);
            }
        }

        for (var index = 0; index < _projects.Count; index++)
        {
            var project = _projects[index];
            if (!_projectRowsById.TryGetValue(project.Id, out var row))
            {
                row = new ProjectListItemViewModel(project);
                _projectRowsById.Add(project.Id, row);
            }

            var existingIndex = ProjectRows.IndexOf(row);
            if (existingIndex < 0)
            {
                ProjectRows.Insert(index, row);
            }
            else if (existingIndex != index)
            {
                ProjectRows.Move(existingIndex, index);
            }

            ProjectRows[index].Refresh();
        }
    }

    private void SynchronizeComparisonRows()
    {
        var comparisons = SelectedProject?.Comparisons ?? [];

        for (var index = SelectedProjectComparisonRows.Count - 1; index >= 0; index--)
        {
            var row = SelectedProjectComparisonRows[index];
            if (!comparisons.Contains(row.Comparison))
            {
                SelectedProjectComparisonRows.RemoveAt(index);
                _comparisonRowsById.Remove(row.Id);
            }
        }

        for (var index = 0; index < comparisons.Count; index++)
        {
            var comparison = comparisons[index];
            if (!_comparisonRowsById.TryGetValue(comparison.Id, out var row))
            {
                row = new ComparisonListItemViewModel(comparison);
                _comparisonRowsById.Add(comparison.Id, row);
            }

            var existingIndex = SelectedProjectComparisonRows.IndexOf(row);
            if (existingIndex < 0)
            {
                SelectedProjectComparisonRows.Insert(index, row);
            }
            else if (existingIndex != index)
            {
                SelectedProjectComparisonRows.Move(existingIndex, index);
            }

            SelectedProjectComparisonRows[index].Refresh();
        }
    }

    private void SelectProject(Project? project)
    {
        var selectedProject = project;
        if ((selectedProject is null || !_projects.Contains(selectedProject)) && _projects.Count > 0)
        {
            selectedProject = _projects.FirstOrDefault();
        }

        var row = selectedProject is null
            ? null
            : _projectRowsById.GetValueOrDefault(selectedProject.Id);
        SelectedProjectRow = row;
    }

    private void SelectComparison(ComparisonSet? comparison)
    {
        var selectedComparison = comparison;
        if (SelectedProject is not null
            && (selectedComparison is null || !SelectedProject.Comparisons.Contains(selectedComparison)))
        {
            selectedComparison = SelectedProject.Comparisons.FirstOrDefault();
        }

        var row = selectedComparison is null
            ? null
            : _comparisonRowsById.GetValueOrDefault(selectedComparison.Id);
        SelectedComparisonRow = row;
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedProjectRowChanged(ProjectListItemViewModel? value)
    {
        RefreshComparisonRows();
        NotifySelectedProjectMutated();
    }

    partial void OnSelectedComparisonRowChanged(ComparisonListItemViewModel? value)
    {
        if (value is not null && (SelectedProject is null || !SelectedProject.Comparisons.Contains(value.Comparison)))
        {
            var repairedRow = SelectedProjectComparisonRows.FirstOrDefault();
            if (!ReferenceEquals(SelectedComparisonRow, repairedRow))
            {
                SelectedComparisonRow = repairedRow;
            }

            return;
        }

        if (value?.Comparison.RequiresReview == true)
        {
            value.Comparison.RequiresReview = false;
            value.Comparison.UpdatedAt = DateTimeOffset.UtcNow;
            value.Refresh();

            if (SelectedProject is not null)
            {
                SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
                _ = SaveProjectAsync(SelectedProject, CancellationToken.None);
            }
        }

        NotifySelectedComparisonMutated();
    }

}
