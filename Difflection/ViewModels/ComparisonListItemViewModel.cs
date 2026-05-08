using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Difflection.Models;

namespace Difflection.ViewModels;

public partial class ComparisonListItemViewModel(ComparisonSet comparison) : ViewModelBase
{
    public ComparisonSet Comparison { get; } = comparison;

    public Guid Id => Comparison.Id;

    public string Name => Comparison.Name;

    public bool IsNotEditing => !IsEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string DraftName { get; set; } = comparison.Name;

    public void BeginEdit()
    {
        DraftName = Comparison.Name;
        IsEditing = true;
    }

    public void CancelEdit()
    {
        DraftName = Comparison.Name;
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
            DraftName = Comparison.Name;
        }
    }
}
