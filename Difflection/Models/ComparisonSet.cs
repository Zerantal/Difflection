using System;
using System.Collections.Generic;

namespace Difflection.Models;

public sealed class ComparisonSet
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "Untitled Comparison";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ReferenceImageId { get; set; }

    public Guid? CandidateImageId { get; set; }

    public List<ImageAsset> Images { get; init; } = [];
}
