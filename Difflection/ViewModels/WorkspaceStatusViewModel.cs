namespace Difflection.ViewModels;

public class WorkspaceStatusViewModel(
    WorkspaceNavigatorViewModel workspace,
    ComparisonDisplayViewModel comparisonDisplay) : ViewModelBase
{
    public bool ShowProjectsEmptyState => workspace.ShowProjectsEmptyState;

    public bool ShowComparisonsEmptyState => workspace.ShowComparisonsEmptyState;

    public string WorkspaceContextTitle
    {
        get
        {
            if (workspace.SelectedProject is null)
            {
                return "No project selected";
            }

            return workspace.SelectedComparison is null
                ? workspace.SelectedProject.Name
                : $"{workspace.SelectedProject.Name} / {workspace.SelectedComparison.Name}";
        }
    }

    public string WorkspaceContextDetail
    {
        get
        {
            if (workspace.SelectedProject is null)
            {
                return "Create or select a project";
            }

            if (workspace.SelectedComparison is null)
            {
                return "No comparison selected";
            }

            return $"{workspace.SelectedComparisonImageCountText} in image set";
        }
    }

    public string WorkspaceActionHint
    {
        get
        {
            if (!workspace.HasProjects)
            {
                return "Create a project to start a workspace.";
            }

            if (workspace.SelectedProject is null)
            {
                return "Select a project to continue.";
            }

            if (workspace.SelectedComparison is null)
            {
                return "Create a comparison for this project.";
            }

            return workspace.SelectedComparison.Images.Count switch
            {
                0 => "Add or drop a baseline image.",
                1 => "Add or drop a candidate image.",
                _ => string.Empty
            };
        }
    }

    public bool ShowWorkspaceActionHint => !string.IsNullOrWhiteSpace(WorkspaceActionHint);

    public bool ShowMainEmptyState => !comparisonDisplay.HasAnyImage && workspace.SelectedComparison?.Images.Count is null or 0;

    public string MainEmptyStateTitle
    {
        get
        {
            if (!workspace.HasProjects)
            {
                return "No projects";
            }

            if (workspace.SelectedProject is null)
            {
                return "No project selected";
            }

            if (workspace.SelectedComparison is null)
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
            if (!workspace.HasProjects)
            {
                return "Create a project, or drop images to create one automatically.";
            }

            if (workspace.SelectedProject is null)
            {
                return "Select a project from the project selector.";
            }

            if (workspace.SelectedComparison is null)
            {
                return "Create a comparison, or drop images to create one automatically.";
            }

            return "Add or drop a baseline image to begin.";
        }
    }

    public void NotifyWorkspaceStateChanged()
    {
        OnPropertyChanged(nameof(ShowProjectsEmptyState));
        OnPropertyChanged(nameof(ShowComparisonsEmptyState));
        NotifyContextChanged();
        NotifyEmptyStateChanged();
    }

    public void NotifyImageStateChanged()
    {
        OnPropertyChanged(nameof(WorkspaceContextDetail));
        OnPropertyChanged(nameof(WorkspaceActionHint));
        OnPropertyChanged(nameof(ShowWorkspaceActionHint));
        NotifyEmptyStateChanged();
    }

    private void NotifyContextChanged()
    {
        OnPropertyChanged(nameof(WorkspaceContextTitle));
        OnPropertyChanged(nameof(WorkspaceContextDetail));
        OnPropertyChanged(nameof(WorkspaceActionHint));
        OnPropertyChanged(nameof(ShowWorkspaceActionHint));
    }

    private void NotifyEmptyStateChanged()
    {
        OnPropertyChanged(nameof(ShowMainEmptyState));
        OnPropertyChanged(nameof(MainEmptyStateTitle));
        OnPropertyChanged(nameof(MainEmptyStateMessage));
    }
}
