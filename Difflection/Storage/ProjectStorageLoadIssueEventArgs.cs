using System;

namespace Difflection.Storage;

public sealed class ProjectStorageLoadIssueEventArgs(
    string projectFilePath,
    Exception exception,
    bool recoveredFromBackup) : EventArgs
{
    public string ProjectFilePath { get; } = projectFilePath;

    public Exception Exception { get; } = exception;

    public bool RecoveredFromBackup { get; } = recoveredFromBackup;
}
