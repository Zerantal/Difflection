using System;
using Difflection.Models;

namespace Difflection.ViewModels;

public class ComparisonListItemViewModel(ComparisonSet comparison) : SidebarListItemViewModel<ComparisonSet>(comparison)
{
    public ComparisonSet Comparison => Model;

    public Guid Id => Comparison.Id;

    public override string Name => Comparison.Name;

    public bool NeedsReview => Comparison.RequiresReview;

    public override string DetailText
    {
        get
        {
            if (Comparison.RequiresReview)
            {
                return "Candidate update pending";
            }

            if (Comparison.Images.Count == 0)
            {
                return "No images";
            }

            if (Comparison.ReferenceImage is null)
            {
                return "Needs baseline";
            }

            if (Comparison.CandidateImage is null)
            {
                return "Needs candidate";
            }

            return "Ready";
        }
    }

    public override void Refresh()
    {
        base.Refresh();
        OnPropertyChanged(nameof(NeedsReview));
    }
}
