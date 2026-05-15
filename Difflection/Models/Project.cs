using System;
using System.Collections.Generic;

namespace Difflection.Models;

public sealed class Project
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "Untitled Project";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ProjectSettings Settings { get; set; } = new();

    public List<ComparisonSet> Comparisons { get; init; } = [];
}

public sealed class ProjectSettings
{
    public bool MonitorSourceFilesForChanges { get; set; }
}
