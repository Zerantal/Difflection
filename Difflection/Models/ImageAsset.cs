using System;

namespace Difflection.Models;

public sealed class ImageAsset
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Label { get; set; } = "Image";

    public string SourceName { get; set; } = string.Empty;

    public string? MediaType { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;

    public ImageSourceMetadata? OriginalFileMetadata { get; set; }

    public ImageMonitoringRole MonitoringRole { get; set; } = ImageMonitoringRole.None;

    public Guid? PreviousVersionImageId { get; set; }
}

public sealed class ImageSourceMetadata
{
    public string? Path { get; set; }

    public string? FileName { get; set; }

    public long? Length { get; set; }

    public DateTimeOffset? LastModifiedAt { get; set; }

    public string? ContentHash { get; set; }
}

public enum ImageMonitoringRole
{
    None,
    Baseline,
    Candidate
}
