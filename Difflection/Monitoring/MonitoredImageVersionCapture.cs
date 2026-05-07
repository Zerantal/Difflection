using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;
using Difflection.Storage;

namespace Difflection.Monitoring;

public sealed class MonitoredImageVersionCapture(IProjectStorage projectStorage)
{
    public async Task<ImageAsset?> CaptureAsync(
        Project project,
        ComparisonSet comparison,
        ImageAsset changedImage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(changedImage);

        if (!project.Comparisons.Contains(comparison)
            || !comparison.Images.Contains(changedImage)
            || changedImage.MonitoringRole == ImageMonitoringRole.None)
        {
            return null;
        }

        var sourcePath = changedImage.OriginalFileMetadata?.Path;

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var sourceMetadata = await ImageSourceMetadataReader.ReadAsync(sourcePath, cancellationToken);

        if (string.Equals(sourceMetadata.ContentHash, changedImage.OriginalFileMetadata?.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var monitoringRole = changedImage.MonitoringRole;
        var version = new ImageAsset
        {
            Label = CreateVersionLabel(changedImage),
            SourceName = Path.GetFileName(sourcePath),
            MediaType = changedImage.MediaType,
            OriginalFileMetadata = sourceMetadata,
            MonitoringRole = monitoringRole,
            PreviousVersionImageId = changedImage.Id
        };

        await using (var stream = File.OpenRead(sourcePath))
        {
            await projectStorage.SaveImageAsync(project.Id, comparison.Id, version, stream, cancellationToken);
        }

        comparison.AddImage(version);
        changedImage.MonitoringRole = ImageMonitoringRole.None;

        switch (monitoringRole)
        {
            case ImageMonitoringRole.Reference:
                comparison.SetReferenceImage(version.Id);
                break;
            case ImageMonitoringRole.Candidate:
                if (comparison.Images.Count >= 2)
                {
                    comparison.SetCandidateImage(version.Id);
                }

                break;
        }

        comparison.UpdatedAt = DateTimeOffset.UtcNow;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await projectStorage.SaveProjectAsync(project, cancellationToken);

        return version;
    }

    private static string CreateVersionLabel(ImageAsset image)
    {
        var label = string.IsNullOrWhiteSpace(image.Label) ? image.SourceName : image.Label;
        return $"{label} version";
    }
}
