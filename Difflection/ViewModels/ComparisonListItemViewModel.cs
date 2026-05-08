using System;
using Difflection.Models;

namespace Difflection.ViewModels;

public partial class ComparisonListItemViewModel(ComparisonSet comparison) : SidebarListItemViewModel<ComparisonSet>(comparison)
{
    public ComparisonSet Comparison => Model;

    public Guid Id => Comparison.Id;

    public override string Name => Comparison.Name;

    public override string DetailText => Comparison.Images.Count == 1
        ? "1 image"
        : $"{Comparison.Images.Count} images";
}
