using System;
using System.Collections.Generic;
using System.Linq;

namespace Difflection.Models;

public sealed class ComparisonSet
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "Untitled Comparison";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ReferenceImageId { get; private set; }

    public Guid? CandidateImageId { get; private set; }

    public List<ImageAsset> Images { get; init; } = [];

    public ImageAsset? ReferenceImage => ReferenceImageId is null
        ? null
        : Images.FirstOrDefault(image => image.Id == ReferenceImageId);

    public ImageAsset? CandidateImage => CandidateImageId is null
        ? null
        : Images.FirstOrDefault(image => image.Id == CandidateImageId);

    public void AddImage(ImageAsset image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (Images.Any(existingImage => existingImage.Id == image.Id))
        {
            throw new InvalidOperationException("The comparison set already contains an image with the same ID.");
        }

        Images.Add(image);
        RepairRoleAssignments();
    }

    public bool RemoveImage(Guid imageId)
    {
        var removed = Images.RemoveAll(image => image.Id == imageId) > 0;

        if (removed)
        {
            RepairRoleAssignments();
        }

        return removed;
    }

    public void SetReferenceImage(Guid imageId)
    {
        ThrowIfImageIsMissing(imageId);

        if (CandidateImageId == imageId)
        {
            CandidateImageId = ReferenceImageId;
        }

        ReferenceImageId = imageId;
        RepairRoleAssignments();
    }

    public void SetCandidateImage(Guid imageId)
    {
        ThrowIfImageIsMissing(imageId);

        if (Images.Count < 2)
        {
            throw new InvalidOperationException("A comparison set needs at least two images before a candidate can be assigned.");
        }

        if (ReferenceImageId == imageId)
        {
            ReferenceImageId = CandidateImageId;
        }

        CandidateImageId = imageId;
        RepairRoleAssignments();
    }

    private void RepairRoleAssignments()
    {
        ReferenceImageId = Images.Any(image => image.Id == ReferenceImageId)
            ? ReferenceImageId
            : Images.FirstOrDefault()?.Id;

        CandidateImageId = Images.Count > 1 && Images.Any(image => image.Id == CandidateImageId)
            ? CandidateImageId
            : Images.FirstOrDefault(image => image.Id != ReferenceImageId)?.Id;

        if (CandidateImageId == ReferenceImageId)
        {
            CandidateImageId = Images.FirstOrDefault(image => image.Id != ReferenceImageId)?.Id;
        }

        if (Images.Count == 0)
        {
            ReferenceImageId = null;
            CandidateImageId = null;
        }
        else if (Images.Count == 1)
        {
            ReferenceImageId = Images[0].Id;
            CandidateImageId = null;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void ThrowIfImageIsMissing(Guid imageId)
    {
        if (Images.All(image => image.Id != imageId))
        {
            throw new ArgumentException("The image is not part of this comparison set.", nameof(imageId));
        }
    }
}
