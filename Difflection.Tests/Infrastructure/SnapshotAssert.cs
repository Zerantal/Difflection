using System;
using System.IO;
using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using Xunit;

namespace Difflection.Tests.Infrastructure;

internal static class SnapshotAssert
{
    public static void Matches(string snapshotName, Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        Matches(snapshotName, stream.ToArray());
    }

    public static void Matches(string snapshotName, byte[] pngBytes)
    {
        var snapshotDirectory = FindSnapshotDirectory();
        var baselinesDirectory = Path.Combine(snapshotDirectory, "Baselines");
        var artifactsDirectory = Path.Combine(snapshotDirectory, "Artifacts");
        Directory.CreateDirectory(baselinesDirectory);
        Directory.CreateDirectory(artifactsDirectory);

        var hashPath = Path.Combine(baselinesDirectory, $"{snapshotName}.sha256");
        var pngPath = Path.Combine(baselinesDirectory, $"{snapshotName}.png");
        var actualPath = Path.Combine(artifactsDirectory, $"{snapshotName}.actual.png");
        var reportPath = Path.Combine(artifactsDirectory, $"{snapshotName}.diff.md");

        var hash = Convert.ToHexString(SHA256.HashData(pngBytes));
        File.WriteAllBytes(actualPath, pngBytes);

        var update = string.Equals(
            Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        if (update || !File.Exists(hashPath))
        {
            File.WriteAllText(hashPath, hash + Environment.NewLine);
            File.WriteAllBytes(pngPath, pngBytes);
            DeleteIfExists(reportPath);
            return;
        }

        var expectedHash = File.ReadAllText(hashPath).Trim();
        if (string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            DeleteIfExists(reportPath);
            return;
        }

        WriteDiffReport(snapshotName, hashPath, pngPath, actualPath, reportPath, expectedHash, hash);

        Assert.Fail(
            $"Snapshot mismatch for '{snapshotName}'. Expected hash={expectedHash}, actual hash={hash}. " +
            $"Wrote actual image to: {actualPath}. Wrote diff report: {reportPath}. " +
            "Set UPDATE_SNAPSHOTS=1 to accept updated snapshots.");
    }

    private static void WriteDiffReport(
        string snapshotName,
        string expectedHashPath,
        string expectedPngPath,
        string actualPngPath,
        string reportPath,
        string expectedHash,
        string actualHash)
    {
        var lines = new[]
        {
            $"# Snapshot Diff: {snapshotName}",
            string.Empty,
            "## Files",
            $"- Expected hash file: `{expectedHashPath}`",
            $"- Expected image: `{expectedPngPath}`",
            $"- Actual image: `{actualPngPath}`",
            string.Empty,
            "## Hashes",
            $"- Expected: `{expectedHash}`",
            $"- Actual: `{actualHash}`",
            string.Empty,
            "## Update",
            "Set `UPDATE_SNAPSHOTS=1` and rerun the test command to accept the new baseline.",
            string.Empty,
        };

        File.WriteAllLines(reportPath, lines);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string FindSnapshotDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "UI");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate snapshot directory.");
    }
}
