using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Difflection.Models;

namespace Difflection.ViewModels;

public partial class ProjectListItemViewModel(Project project) : ViewModelBase
{
    public Project Project { get; } = project;

    public Guid Id => Project.Id;

    public string Name => Project.Name;

    public bool IsNotEditing => !IsEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string DraftName { get; set; } = project.Name;

    public void BeginEdit()
    {
        DraftName = Project.Name;
        IsEditing = true;
    }

    public void CancelEdit()
    {
        DraftName = Project.Name;
        IsEditing = false;
    }

    public void EndEdit()
    {
        IsEditing = false;
        NotifyNameChanged();
    }

    public void NotifyNameChanged()
    {
        OnPropertyChanged(nameof(Name));
        if (!IsEditing)
        {
            DraftName = Project.Name;
        }
    }
}
