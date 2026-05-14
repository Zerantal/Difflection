using CommunityToolkit.Mvvm.ComponentModel;

namespace Difflection.ViewModels;

public abstract partial class SidebarListItemViewModel<TModel>(TModel model) : ViewModelBase
    where TModel : class
{
    public TModel Model { get; } = model;

    public abstract string Name { get; }

    public virtual string DetailText => string.Empty;

    public bool IsNotEditing => !IsEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string DraftName { get; set; } = string.Empty;

    public void BeginEdit()
    {
        DraftName = Name;
        IsEditing = true;
    }

    public void CancelEdit()
    {
        DraftName = Name;
        IsEditing = false;
    }

    public void EndEdit()
    {
        IsEditing = false;
        Refresh();
    }

    public virtual void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DetailText));
        if (!IsEditing)
        {
            DraftName = Name;
        }
    }
}
