using System;
using Difflection.Models;
using Xunit;

namespace Difflection.Tests.Models;

public sealed class ComparisonSetRoleTests
{
    [Fact]
    public void AddImage_assigns_first_image_as_reference_only()
    {
        var comparison = new ComparisonSet();
        var image = CreateImage("reference.png");

        comparison.AddImage(image);

        Assert.Equal(image.Id, comparison.ReferenceImageId);
        Assert.Null(comparison.CandidateImageId);
        Assert.Same(image, comparison.ReferenceImage);
        Assert.Null(comparison.CandidateImage);
    }

    [Fact]
    public void AddImage_assigns_second_image_as_candidate()
    {
        var comparison = new ComparisonSet();
        var reference = CreateImage("reference.png");
        var candidate = CreateImage("candidate.png");

        comparison.AddImage(reference);
        comparison.AddImage(candidate);

        Assert.Equal(reference.Id, comparison.ReferenceImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
    }

    [Fact]
    public void SetCandidateImage_swaps_roles_when_candidate_is_current_reference()
    {
        var comparison = new ComparisonSet();
        var reference = CreateImage("reference.png");
        var candidate = CreateImage("candidate.png");
        comparison.AddImage(reference);
        comparison.AddImage(candidate);

        comparison.SetCandidateImage(reference.Id);

        Assert.Equal(candidate.Id, comparison.ReferenceImageId);
        Assert.Equal(reference.Id, comparison.CandidateImageId);
    }

    [Fact]
    public void SetReferenceImage_swaps_roles_when_reference_is_current_candidate()
    {
        var comparison = new ComparisonSet();
        var reference = CreateImage("reference.png");
        var candidate = CreateImage("candidate.png");
        comparison.AddImage(reference);
        comparison.AddImage(candidate);

        comparison.SetReferenceImage(candidate.Id);

        Assert.Equal(candidate.Id, comparison.ReferenceImageId);
        Assert.Equal(reference.Id, comparison.CandidateImageId);
    }

    [Fact]
    public void RemoveImage_repairs_roles_after_candidate_is_deleted()
    {
        var comparison = new ComparisonSet();
        var reference = CreateImage("reference.png");
        var candidate = CreateImage("candidate.png");
        var alternate = CreateImage("alternate.png");
        comparison.AddImage(reference);
        comparison.AddImage(candidate);
        comparison.AddImage(alternate);

        var removed = comparison.RemoveImage(candidate.Id);

        Assert.True(removed);
        Assert.Equal(reference.Id, comparison.ReferenceImageId);
        Assert.Equal(alternate.Id, comparison.CandidateImageId);
    }

    [Fact]
    public void RemoveImage_repairs_roles_after_reference_is_deleted()
    {
        var comparison = new ComparisonSet();
        var reference = CreateImage("reference.png");
        var candidate = CreateImage("candidate.png");
        comparison.AddImage(reference);
        comparison.AddImage(candidate);

        var removed = comparison.RemoveImage(reference.Id);

        Assert.True(removed);
        Assert.Equal(candidate.Id, comparison.ReferenceImageId);
        Assert.Null(comparison.CandidateImageId);
    }

    [Fact]
    public void SetCandidateImage_requires_at_least_two_images()
    {
        var comparison = new ComparisonSet();
        var reference = CreateImage("reference.png");
        comparison.AddImage(reference);

        var exception = Assert.Throws<InvalidOperationException>(() => comparison.SetCandidateImage(reference.Id));

        Assert.Contains("at least two images", exception.Message);
    }

    [Fact]
    public void SetReferenceImage_requires_image_to_belong_to_set()
    {
        var comparison = new ComparisonSet();

        var exception = Assert.Throws<ArgumentException>(() => comparison.SetReferenceImage(Guid.NewGuid()));

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
