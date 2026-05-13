using System;
using Avalonia;
using Avalonia.Media;
using Difflection.Models;

namespace Difflection.ViewModels;

public class ComparisonListItemViewModel(ComparisonSet comparison) : SidebarListItemViewModel<ComparisonSet>(comparison)
{
    private static readonly IBrush TransparentBrush = Brushes.Transparent;
    private static readonly IBrush NeedsReviewBackgroundBrush = new SolidColorBrush(Color.Parse("#221F14"));
    private static readonly IBrush NeedsReviewBorderBrush = new SolidColorBrush(Color.Parse("#A16207"));
    private static readonly Thickness NoReviewBorderThickness = new(0);
    private static readonly Thickness NeedsReviewBorderThickness = new(1);

    public ComparisonSet Comparison => Model;

    public Guid Id => Comparison.Id;

    public override string Name => Comparison.Name;

    public bool NeedsReview => Comparison.RequiresReview;

    public IBrush ReviewBackground => NeedsReview ? NeedsReviewBackgroundBrush : TransparentBrush;

    public IBrush ReviewBorderBrush => NeedsReview ? NeedsReviewBorderBrush : TransparentBrush;

    public Thickness ReviewBorderThickness => NeedsReview ? NeedsReviewBorderThickness : NoReviewBorderThickness;

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
        OnPropertyChanged(nameof(ReviewBackground));
        OnPropertyChanged(nameof(ReviewBorderBrush));
        OnPropertyChanged(nameof(ReviewBorderThickness));
    }
}
