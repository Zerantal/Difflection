using System;
using Difflection.Models;
using Xunit;

namespace Difflection.Tests.Models;

public sealed class ProjectModelTests
{
    [Fact]
    public void Project_defaults_to_an_empty_workspace()
    {
        var project = new Project();

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("Untitled Project", project.Name);
        Assert.Empty(project.Comparisons);
    }

    [Fact]
    public void Comparison_tracks_images_and_role_assignments_by_id()
    {
        var reference = new ImageAsset { Label = "Baseline", SourceName = "baseline.png", StorageKey = "images/baseline.png" };
        var candidate = new ImageAsset { Label = "Candidate", SourceName = "candidate.png", StorageKey = "images/candidate.png" };
        var comparison = new ComparisonSet
        {
            Name = "Landing Page",
            ReferenceImageId = reference.Id,
            CandidateImageId = candidate.Id,
            Images = { reference, candidate }
        };

        Assert.Equal("Landing Page", comparison.Name);
        Assert.Equal(reference.Id, comparison.ReferenceImageId);
        Assert.Equal(candidate.Id, comparison.CandidateImageId);
        Assert.Collection(
            comparison.Images,
            image => Assert.Equal("Baseline", image.Label),
            image => Assert.Equal("Candidate", image.Label));
    }

    [Fact]
    public void Image_keeps_storage_and_source_metadata_separate()
    {
        var image = new ImageAsset
        {
            Label = "Revision 2",
            SourceName = "output.png",
            MediaType = "image/png",
            StorageKey = "project-1/images/image-1.png",
            MonitoringRole = ImageMonitoringRole.Candidate,
            OriginalFileMetadata = new ImageSourceMetadata
            {
                Path = "/work/output.png",
                FileName = "output.png",
                Length = 1024,
                LastModifiedAt = DateTimeOffset.UnixEpoch,
                ContentHash = "abc123"
            }
        };

        Assert.Equal("project-1/images/image-1.png", image.StorageKey);
        Assert.Equal(ImageMonitoringRole.Candidate, image.MonitoringRole);
        Assert.Equal("abc123", image.OriginalFileMetadata?.ContentHash);
    }
}
