using System;
using System.IO;
using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Xunit;

namespace Difflection.Tests.Infrastructure;

internal static class SnapshotAssert
{
    private const int ChannelTolerance = 3;
    private const double DifferencePixelRatioTolerance = 0;// 0.005;

    public static void Matches(string snapshotName, Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        Matches(snapshotName, stream.ToArray());
    }

    private static void Matches(string snapshotName, byte[] pngBytes)
    {
        var snapshotDirectory = FindSnapshotDirectory();
        var baselinesDirectory = Path.Combine(snapshotDirectory, "Baselines");
        var artifactsDirectory = Path.Combine(snapshotDirectory, "Artifacts");
        Directory.CreateDirectory(baselinesDirectory);
        Directory.CreateDirectory(artifactsDirectory);

        var hashPath = Path.Combine(baselinesDirectory, $"{snapshotName}.sha256");
        var pngPath = Path.Combine(baselinesDirectory, $"{snapshotName}.png");
        var actualPath = Path.Combine(artifactsDirectory, $"{snapshotName}.actual.png");
        var diffImagePath = Path.Combine(artifactsDirectory, $"{snapshotName}.diff.png");
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
            DeleteIfExists(diffImagePath);
            DeleteIfExists(reportPath);
            return;
        }

        var expectedHash = File.ReadAllText(hashPath).Trim();
        if (string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            DeleteIfExists(diffImagePath);
            DeleteIfExists(reportPath);
            return;
        }

        var comparison = ComparePngs(pngPath, actualPath, diffImagePath);
        if (comparison.IsWithinTolerance)
        {
            DeleteIfExists(diffImagePath);
            DeleteIfExists(reportPath);
            return;
        }

        WriteDiffReport(snapshotName, hashPath, pngPath, actualPath, diffImagePath, reportPath, expectedHash, hash, comparison);

        Assert.Fail(
            $"Snapshot mismatch for '{snapshotName}'. Expected hash={expectedHash}, actual hash={hash}. " +
            $"Changed pixels={comparison.DifferentPixelRatio:P3}, max channel delta={comparison.MaxChannelDelta}. " +
            $"Wrote actual image to: {actualPath}. Wrote diff image to: {diffImagePath}. Wrote diff report: {reportPath}. " +
            "Set UPDATE_SNAPSHOTS=1 to accept updated snapshots.");
    }

    private static void WriteDiffReport(
        string snapshotName,
        string expectedHashPath,
        string expectedPngPath,
        string actualPngPath,
        string diffImagePath,
        string reportPath,
        string expectedHash,
        string actualHash,
        SnapshotComparison comparison)
    {
        var lines = new[]
        {
            $"# Snapshot Diff: {snapshotName}",
            string.Empty,
            "## Files",
            $"- Expected hash file: `{expectedHashPath}`",
            $"- Expected image: `{expectedPngPath}`",
            $"- Actual image: `{actualPngPath}`",
            $"- Diff image: `{diffImagePath}`",
            string.Empty,
            "## Hashes",
            $"- Expected: `{expectedHash}`",
            $"- Actual: `{actualHash}`",
            string.Empty,
            "## Pixel Difference",
            $"- Size: `{comparison.Width}x{comparison.Height}`",
            $"- Changed pixels above channel tolerance `{ChannelTolerance}`: `{comparison.DifferentPixels}` / `{comparison.TotalPixels}` (`{comparison.DifferentPixelRatio:P3}`)",
            $"- Max channel delta: `{comparison.MaxChannelDelta}`",
            $"- Average channel delta: `{comparison.AverageChannelDelta:0.###}`",
            $"- Allowed changed pixel ratio: `{DifferencePixelRatioTolerance:P3}`",
            string.Empty,
            "## Update",
            "Set `UPDATE_SNAPSHOTS=1` and rerun the test command to accept the new baseline.",
            string.Empty
        };

        File.WriteAllLines(reportPath, lines);
    }

    private static SnapshotComparison ComparePngs(string expectedPngPath, string actualPngPath, string diffImagePath)
    {
        using var expected = SKBitmap.Decode(expectedPngPath);
        using var actual = SKBitmap.Decode(actualPngPath);
        if (expected is null || actual is null)
        {
            DeleteIfExists(diffImagePath);
            return SnapshotComparison.FailedDecode;
        }

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            WriteSizeMismatchDiffImage(expected, actual, diffImagePath);
            return SnapshotComparison.SizeMismatch(expected.Width, expected.Height);
        }

        var differentPixels = 0;
        var maxChannelDelta = 0;
        long totalChannelDelta = 0;
        var totalPixels = expected.Width * expected.Height;
        using var diff = new SKBitmap(expected.Width, expected.Height);

        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var expectedPixel = expected.GetPixel(x, y);
                var actualPixel = actual.GetPixel(x, y);
                var redDelta = Math.Abs(expectedPixel.Red - actualPixel.Red);
                var greenDelta = Math.Abs(expectedPixel.Green - actualPixel.Green);
                var blueDelta = Math.Abs(expectedPixel.Blue - actualPixel.Blue);
                var alphaDelta = Math.Abs(expectedPixel.Alpha - actualPixel.Alpha);
                var pixelMaxDelta = Math.Max(Math.Max(redDelta, greenDelta), Math.Max(blueDelta, alphaDelta));

                maxChannelDelta = Math.Max(maxChannelDelta, pixelMaxDelta);
                totalChannelDelta += redDelta + greenDelta + blueDelta + alphaDelta;

                if (pixelMaxDelta > ChannelTolerance)
                {
                    differentPixels++;
                    diff.SetPixel(x, y, CreateChangedPixel(pixelMaxDelta));
                }
                else
                {
                    diff.SetPixel(x, y, CreateUnchangedPixel(actualPixel));
                }
            }
        }

        WritePng(diff, diffImagePath);

        var differentPixelRatio = (double)differentPixels / Math.Max(1, totalPixels);
        var averageChannelDelta = (double)totalChannelDelta / Math.Max(1, totalPixels * 4);
        return new SnapshotComparison(
            expected.Width,
            expected.Height,
            totalPixels,
            differentPixels,
            differentPixelRatio,
            maxChannelDelta,
            averageChannelDelta,
            differentPixelRatio <= DifferencePixelRatioTolerance);
    }

    private static SKColor CreateChangedPixel(int pixelMaxDelta)
    {
        var intensity = (byte)Math.Clamp(96 + pixelMaxDelta, 96, 255);
        return new SKColor(255, (byte)(255 - intensity), (byte)(255 - intensity), 255);
    }

    private static SKColor CreateUnchangedPixel(SKColor actualPixel)
    {
        var gray = (byte)((actualPixel.Red * 0.299 + actualPixel.Green * 0.587 + actualPixel.Blue * 0.114) * 0.25);
        return new SKColor(gray, gray, gray, actualPixel.Alpha);
    }

    private static void WriteSizeMismatchDiffImage(SKBitmap expected, SKBitmap actual, string diffImagePath)
    {
        var width = Math.Max(expected.Width, actual.Width);
        var height = Math.Max(expected.Height, actual.Height);
        using var diff = new SKBitmap(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var hasExpected = x < expected.Width && y < expected.Height;
                var hasActual = x < actual.Width && y < actual.Height;

                diff.SetPixel(x, y, (hasExpected, hasActual) switch
                {
                    (true, true) => CreateChangedPixel(255),
                    (true, false) => new SKColor(255, 128, 0),
                    (false, true) => new SKColor(0, 128, 255),
                    _ => SKColors.Transparent
                });
            }
        }

        WritePng(diff, diffImagePath);
    }

    private static void WritePng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
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

    private sealed record SnapshotComparison(
        int Width,
        int Height,
        int TotalPixels,
        int DifferentPixels,
        double DifferentPixelRatio,
        int MaxChannelDelta,
        double AverageChannelDelta,
        bool IsWithinTolerance)
    {
        public static SnapshotComparison FailedDecode { get; } = new(0, 0, 0, int.MaxValue, 1, 255, 255, false);

        public static SnapshotComparison SizeMismatch(int width, int height) => new(width, height, 0, int.MaxValue, 1, 255, 255, false);
    }
}
