using System;
using Difflection.Models;

namespace Difflection.ViewModels;

public partial class ProjectListItemViewModel(Project project) : SidebarListItemViewModel<Project>(project)
{
    public Project Project => Model;

    public Guid Id => Project.Id;

    public override string Name => Project.Name;

    public override string DetailText => Project.Comparisons.Count == 1
        ? "1 comparison"
        : $"{Project.Comparisons.Count} comparisons";
}
