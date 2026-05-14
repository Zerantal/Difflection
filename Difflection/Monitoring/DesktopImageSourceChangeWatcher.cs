using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Difflection.Monitoring;

public sealed class DesktopImageSourceChangeWatcher : IImageSourceChangeWatcher
{
    private readonly Dictionary<string, string> _sourceIdsByPath = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileSystemWatcher> _watchersByDirectory = new(StringComparer.Ordinal);

    public event EventHandler<ImageSourceChangedEventArgs>? SourceChanged;

    public void Watch(IEnumerable<ImageSourceWatch> sources)
    {
        Stop();

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.LocalPath) || !File.Exists(source.LocalPath))
            {
                continue;
            }

            var path = Path.GetFullPath(source.LocalPath);
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            _sourceIdsByPath[path] = source.SourceId;

            if (!_watchersByDirectory.ContainsKey(directory))
            {
                _watchersByDirectory[directory] = CreateWatcher(directory);
            }
        }

        foreach (var watcher in _watchersByDirectory.Values)
        {
            watcher.EnableRaisingEvents = true;
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchersByDirectory.Values)
        {
            watcher.Dispose();
        }

        _watchersByDirectory.Clear();
        _sourceIdsByPath.Clear();
    }

    public void Dispose()
    {
        Stop();
    }

    internal void RaiseChanged(string path)
    {
        path = Path.GetFullPath(path);

        if (_sourceIdsByPath.TryGetValue(path, out var sourceId))
        {
            SourceChanged?.Invoke(this, new ImageSourceChangedEventArgs(sourceId));
        }
    }

    private FileSystemWatcher CreateWatcher(string directory)
    {
        var watcher = new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        watcher.Changed += FileChanged;
        watcher.Created += FileChanged;
        watcher.Renamed += FileRenamed;

        return watcher;
    }

    private void FileChanged(object sender, FileSystemEventArgs e)
    {
        RaiseChanged(e.FullPath);
    }

    private void FileRenamed(object sender, RenamedEventArgs e)
    {
        foreach (var path in new[] { e.FullPath, e.OldFullPath }.Distinct(StringComparer.Ordinal))
        {
            RaiseChanged(path);
        }
    }
}
