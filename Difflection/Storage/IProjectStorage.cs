using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;

namespace Difflection.Storage;

public interface IProjectStorage
{
    Task<IReadOnlyList<Project>> LoadProjectsAsync(CancellationToken cancellationToken = default);

    Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveProjectAsync(Project project, CancellationToken cancellationToken = default);

    Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<string> SaveImageAsync(
        Guid projectId,
        Guid comparisonSetId,
        ImageAsset image,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> LoadImageAsync(ImageAsset image, CancellationToken cancellationToken = default);

    Task DeleteImageAsync(ImageAsset image, CancellationToken cancellationToken = default);
}
