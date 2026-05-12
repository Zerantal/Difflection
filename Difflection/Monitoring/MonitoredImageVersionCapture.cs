using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;
using Difflection.Storage;

namespace Difflection.Monitoring;

public sealed class MonitoredImageVersionCapture(IProjectStorage projectStorage)
{
    /// <summary>
    /// Captures a new persisted image version for a monitored image when its source file content has changed.
    /// </summary>
    /// <param name="project">The project that owns the comparison.</param>
    /// <param name="comparison">The comparison that contains the changed image.</param>
    /// <param name="changedImage">The monitored image whose source file may have changed.</param>
    /// <param name="cancellationToken">A token used to cancel storage and file operations.</param>
    /// <returns>The newly captured image version, or <c>null</c> when the image cannot be refreshed or the source hash is unchanged.</returns>
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

        return await CaptureVersionAsync(project, comparison, changedImage, sourcePath, sourceMetadata, changedImage.MonitoringRole, cancellationToken);
    }

    /// <summary>
    /// Refreshes a current reference or candidate image from its source file when the file is newer and its content hash differs.
    /// </summary>
    /// <param name="project">The project that owns the comparison.</param>
    /// <param name="comparison">The comparison that contains the current role image.</param>
    /// <param name="image">The image currently assigned to the supplied role.</param>
    /// <param name="role">The role to refresh; only reference and candidate roles are supported.</param>
    /// <param name="cancellationToken">A token used to cancel storage and file operations.</param>
    /// <returns>The newly captured image version, or <c>null</c> when no newer changed source file is available.</returns>
    public async Task<ImageAsset?> RefreshCurrentRoleImageAsync(
        Project project,
        ComparisonSet comparison,
        ImageAsset image,
        ImageMonitoringRole role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(image);

        if (!project.Comparisons.Contains(comparison) || !comparison.Images.Contains(image))
        {
            return null;
        }

        if (role == ImageMonitoringRole.Reference && comparison.ReferenceImageId != image.Id)
        {
            return null;
        }

        if (role == ImageMonitoringRole.Candidate && comparison.CandidateImageId != image.Id)
        {
            return null;
        }

        if (role is not (ImageMonitoringRole.Reference or ImageMonitoringRole.Candidate))
        {
            return null;
        }

        var sourcePath = image.OriginalFileMetadata?.Path;

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var previousLastModified = image.OriginalFileMetadata?.LastModifiedAt;

        if (previousLastModified is null)
        {
            return null;
        }

        var sourceMetadata = await ImageSourceMetadataReader.ReadAsync(sourcePath, cancellationToken);

        if (sourceMetadata.LastModifiedAt <= previousLastModified)
        {
            return null;
        }

        if (string.Equals(sourceMetadata.ContentHash, image.OriginalFileMetadata?.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var monitoringRole = image.MonitoringRole == ImageMonitoringRole.None
            ? ImageMonitoringRole.None
            : role;

        return await CaptureVersionAsync(project, comparison, image, sourcePath, sourceMetadata, monitoringRole, cancellationToken);
    }

    /// <summary>
    /// Persists a source file as a new image asset, appends it to the comparison, and assigns it to the appropriate role.
    /// </summary>
    /// <param name="project">The project that owns the comparison.</param>
    /// <param name="comparison">The comparison that receives the new image version.</param>
    /// <param name="changedImage">The previous image version being superseded.</param>
    /// <param name="sourcePath">The local source file path to capture.</param>
    /// <param name="sourceMetadata">Metadata read from the current source file.</param>
    /// <param name="monitoringRole">The monitoring role to preserve on the new version, if any.</param>
    /// <param name="cancellationToken">A token used to cancel storage and file operations.</param>
    /// <returns>The newly captured image version.</returns>
    private async Task<ImageAsset?> CaptureVersionAsync(
        Project project,
        ComparisonSet comparison,
        ImageAsset changedImage,
        string sourcePath,
        ImageSourceMetadata sourceMetadata,
        ImageMonitoringRole monitoringRole,
        CancellationToken cancellationToken)
    {
        var version = new ImageAsset
        {
            Label = "Image",
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
            case ImageMonitoringRole.None:
                if (comparison.ReferenceImageId == changedImage.Id)
                {
                    comparison.SetReferenceImage(version.Id);
                }
                else if (comparison.CandidateImageId == changedImage.Id && comparison.Images.Count >= 2)
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
}
