using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Difflection.Models;

namespace Difflection.Monitoring;

public static class ImageSourceMetadataReader
{
    public static async Task<ImageSourceMetadata?> ReadAsync(
        IStorageFile file,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var path = file.Path.IsFile ? file.Path.LocalPath : null;
        var metadata = string.IsNullOrWhiteSpace(path)
            ? new ImageSourceMetadata { FileName = file.Name }
            : await ReadAsync(path, cancellationToken);

        metadata.FileName = file.Name;

        if (metadata.ContentHash is null && stream.CanSeek)
        {
            metadata.Length = stream.Length;
            metadata.ContentHash = await ComputeHashAsync(stream, cancellationToken);
        }

        return metadata;
    }

    public static async Task<ImageSourceMetadata> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(path);
        await using var stream = File.OpenRead(path);

        return new ImageSourceMetadata
        {
            Path = path,
            FileName = fileInfo.Name,
            Length = fileInfo.Length,
            LastModifiedAt = fileInfo.LastWriteTimeUtc,
            ContentHash = await ComputeHashAsync(stream, cancellationToken)
        };
    }

    private static async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken)
    {
        var originalPosition = stream.CanSeek ? stream.Position : 0;

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = originalPosition;
        }

        return Convert.ToHexString(hashBytes);
    }
}
