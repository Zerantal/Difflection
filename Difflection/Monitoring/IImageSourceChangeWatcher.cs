using System;
using System.Collections.Generic;

namespace Difflection.Monitoring;

public interface IImageSourceChangeWatcher : IDisposable
{
    event EventHandler<ImageSourceChangedEventArgs>? SourceChanged;

    void Watch(IEnumerable<ImageSourceWatch> sources);

    void Stop();
}

public sealed record ImageSourceWatch(string SourceId, string? LocalPath);

public sealed record ImageSourceChangedEventArgs(string SourceId);
