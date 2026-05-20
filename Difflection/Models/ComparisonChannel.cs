using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Difflection.Models;

public sealed class ComparisonChannel
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "Channel";

    public ImageMonitoringRole Role { get; init; }

    public Guid? ActiveImageId { get; private set; }

    public List<ImageAsset> Images { get; init; } = [];

    [JsonIgnore]
    public ImageAsset? ActiveImage => ActiveImageId is null
        ? Images.OrderByDescending(image => image.AddedAt).ThenByDescending(image => image.Id).FirstOrDefault()
        : Images.FirstOrDefault(image => image.Id == ActiveImageId)
          ?? Images.OrderByDescending(image => image.AddedAt).ThenByDescending(image => image.Id).FirstOrDefault();

    public bool Contains(ImageAsset image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Images.Contains(image);
    }

    public bool Contains(Guid imageId)
    {
        return Images.Any(image => image.Id == imageId);
    }

    public void AddImage(ImageAsset image, bool makeActive = true)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (Contains(image.Id))
        {
            throw new InvalidOperationException("The channel already contains an image with the same ID.");
        }

        Images.Add(image);

        if (makeActive || ActiveImageId is null)
        {
            ActiveImageId = image.Id;
        }
    }

    public bool RemoveImage(Guid imageId)
    {
        var removed = Images.RemoveAll(image => image.Id == imageId) > 0;

        if (removed)
        {
            RepairActiveImage();
        }

        return removed;
    }

    public void SetActiveImage(Guid imageId)
    {
        if (!Contains(imageId))
        {
            throw new ArgumentException("The image is not part of this channel.", nameof(imageId));
        }

        ActiveImageId = imageId;
    }

    public void RepairActiveImage()
    {
        ActiveImageId = Images.Any(image => image.Id == ActiveImageId)
            ? ActiveImageId
            : Images.OrderByDescending(image => image.AddedAt).ThenByDescending(image => image.Id).FirstOrDefault()?.Id;
    }
}
