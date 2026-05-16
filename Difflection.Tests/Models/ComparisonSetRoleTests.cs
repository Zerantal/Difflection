using System;
using Difflection.Models;
using Xunit;

namespace Difflection.Tests.Models;

public sealed class ComparisonSetRoleTests
{
    [Fact]
    public void AddImage_assigns_first_image_to_baseline_channel()
    {
        var comparison = new ComparisonSet();
        var image = CreateImage("reference.png");

        comparison.AddImage(image);

        Assert.Equal(image.Id, comparison.BaselineImageId);
        Assert.Null(comparison.CandidateImageId);
        Assert.Same(image, comparison.BaselineImage);
        Assert.Null(comparison.CandidateImage);
        Assert.Contains(image, comparison.BaselineChannel.Images);
        Assert.Empty(comparison.CandidateChannel.Images);
    }

    [Fact]
    public void AddImage_assigns_second_image_as_candidate()
    {
        var comparison = new ComparisonSet();
        var baseline = CreateImage("baseline.png");
        var candidate = CreateImage("candidate.png");

        comparison.AddImage(baseline);
        comparison.AddImage(candidate);

        Assert.Equal(baseline.Id, comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
        Assert.Contains(candidate, comparison.CandidateChannel.Images);
    }

    [Fact]
    public void SetCandidateImage_sets_active_candidate_revision()
    {
        var comparison = new ComparisonSet();
        var baseline = CreateImage("baseline.png");
        var candidate = CreateImage("candidate.png");
        var alternate = CreateImage("alternate.png");
        comparison.AddImage(baseline);
        comparison.AddImage(candidate);
        comparison.AddImage(alternate);

        comparison.SetCandidateImage(candidate.Id);

        Assert.Equal(baseline.Id, comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
    }

    [Fact]
    public void SetBaselineImage_sets_active_baseline_revision()
    {
        var comparison = new ComparisonSet();
        var baseline = CreateImage("baseline.png");
        var baselineRevision = CreateImage("baseline-r2.png");
        var candidate = CreateImage("candidate.png");
        comparison.AddImage(baseline);
        comparison.AddImage(candidate);
        comparison.AddImageToChannel(comparison.BaselineChannel, baselineRevision);

        comparison.SetBaselineImage(baseline.Id);

        Assert.Equal(baseline.Id, comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
    }

    [Fact]
    public void RemoveImage_repairs_roles_after_candidate_is_deleted()
    {
        var comparison = new ComparisonSet();
        var baseline = CreateImage("baseline.png");
        var candidate = CreateImage("candidate.png");
        var alternate = CreateImage("alternate.png");
        comparison.AddImage(baseline);
        comparison.AddImage(candidate);
        comparison.AddImage(alternate);

        var removed = comparison.RemoveImage(candidate.Id);

        Assert.True(removed);
        Assert.Equal(baseline.Id, comparison.BaselineImageId);
        Assert.Equal(alternate.Id, comparison.CandidateImageId);
    }

    [Fact]
    public void RemoveImage_clears_baseline_when_last_baseline_revision_is_deleted()
    {
        var comparison = new ComparisonSet();
        var baseline = CreateImage("baseline.png");
        var candidate = CreateImage("candidate.png");
        comparison.AddImage(baseline);
        comparison.AddImage(candidate);

        var removed = comparison.RemoveImage(baseline.Id);

        Assert.True(removed);
        Assert.Null(comparison.BaselineImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
        Assert.Equal(candidate.Id, comparison.CandidateChannel.ActiveImageId);
    }

    [Fact]
    public void AddImageToChannel_adds_revision_and_sets_active_image()
    {
        var comparison = new ComparisonSet();
        var baseline = CreateImage("baseline.png");
        var candidate = CreateImage("candidate.png");
        comparison.AddImage(baseline);
        comparison.AddImage(candidate);
        var baselineVersion = CreateImage("baseline.png");
        baselineVersion.PreviousVersionImageId = baseline.Id;

        comparison.AddImageToChannel(comparison.BaselineChannel, baselineVersion);

        Assert.Contains(baselineVersion, comparison.BaselineChannel.Images);
        Assert.Equal(baselineVersion.Id, comparison.BaselineImageId);
        Assert.Same(baselineVersion, comparison.BaselineImage);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
    }

    [Fact]
    public void SetCandidateImage_requires_image_to_belong_to_candidate_channel()
    {
        var comparison = new ComparisonSet();
        var baseline = CreateImage("baseline.png");
        comparison.AddImage(baseline);

        var exception = Assert.Throws<ArgumentException>(() => comparison.SetCandidateImage(baseline.Id));

        Assert.Equal("imageId", exception.ParamName);
    }

    [Fact]
    public void SetBaselineImage_requires_image_to_belong_to_set()
    {
        var comparison = new ComparisonSet();

        var exception = Assert.Throws<ArgumentException>(() => comparison.SetBaselineImage(Guid.NewGuid()));

        Assert.Equal("imageId", exception.ParamName);
    }

    private static ImageAsset CreateImage(string sourceName)
    {
        return new ImageAsset
        {
            Label = sourceName,
            SourceName = sourceName,
            StorageKey = $"images/{sourceName}"
        };
    }
}
