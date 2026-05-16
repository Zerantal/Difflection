using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Difflection.Models;

public sealed class ComparisonSet
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "Untitled Comparison";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool RequiresReview { get; set; }

    public ComparisonChannel BaselineChannel { get; init; } = new()
    {
        Name = "Baseline",
        Role = ImageMonitoringRole.Baseline
    };

    public ComparisonChannel CandidateChannel { get; init; } = new()
    {
        Name = "Candidate",
        Role = ImageMonitoringRole.Candidate
    };

    [JsonIgnore]
    public IReadOnlyList<ImageAsset> Images => BaselineChannel.Images.Concat(CandidateChannel.Images).ToArray();

    [JsonIgnore]
    public ImageAsset? BaselineImage => BaselineChannel.ActiveImage;

    [JsonIgnore]
    public ImageAsset? CandidateImage => CandidateChannel.ActiveImage;

    [JsonIgnore]
    public Guid? BaselineImageId => BaselineChannel.ActiveImageId;

    [JsonIgnore]
    public Guid? CandidateImageId => CandidateChannel.ActiveImageId;

    public void AddImage(ImageAsset image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (Images.Any(existingImage => existingImage.Id == image.Id))
        {
            throw new InvalidOperationException("The comparison set already contains an image with the same ID.");
        }

        GetDefaultChannelForNewImage().AddImage(image);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddImageToChannel(ComparisonChannel channel, ImageAsset image, bool makeActive = true)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(image);

        if (!ReferenceEquals(channel, BaselineChannel) && !ReferenceEquals(channel, CandidateChannel))
        {
            throw new ArgumentException("The channel is not part of this comparison set.", nameof(channel));
        }

        if (Images.Any(existingImage => existingImage.Id == image.Id))
        {
            throw new InvalidOperationException("The comparison set already contains an image with the same ID.");
        }

        channel.AddImage(image, makeActive);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool RemoveImage(Guid imageId)
    {
        var removed = BaselineChannel.RemoveImage(imageId) || CandidateChannel.RemoveImage(imageId);

        if (removed)
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        return removed;
    }

    public void SetBaselineImage(Guid imageId)
    {
        BaselineChannel.SetActiveImage(imageId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetCandidateImage(Guid imageId)
    {
        CandidateChannel.SetActiveImage(imageId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public ComparisonChannel? GetChannelForImage(ImageAsset image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return GetChannelForImage(image.Id);
    }

    public ComparisonChannel? GetChannelForImage(Guid imageId)
    {
        if (BaselineChannel.Contains(imageId))
        {
            return BaselineChannel;
        }

        return CandidateChannel.Contains(imageId) ? CandidateChannel : null;
    }

    public ComparisonChannel? GetChannelForRole(ImageMonitoringRole role)
    {
        return role switch
        {
            ImageMonitoringRole.Baseline => BaselineChannel,
            ImageMonitoringRole.Candidate => CandidateChannel,
            _ => null
        };
    }

    public void RepairChannelAssignments(bool updateTimestamp = true)
    {
        BaselineChannel.RepairActiveImage();
        CandidateChannel.RepairActiveImage();

        if (updateTimestamp)
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private ComparisonChannel GetDefaultChannelForNewImage()
    {
        return BaselineChannel.Images.Count == 0 ? BaselineChannel : CandidateChannel;
    }
}
