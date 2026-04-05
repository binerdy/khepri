// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Stores each project as a JSON sidecar next to its frame images.
///
/// Layout on device (inside the user-selected root folder):
///   &lt;root&gt;/
///     projects/
///       {projectId}/
///         project.json      ← metadata
///         frame_0.jpg
///         frame_1.jpg
///         ...
/// </summary>
public sealed class JsonTimelapseRepository : ITimelapseRepository
{
    // Serialises all file I/O so concurrent reads/writes never contend on the
    // same file.  A single lock is fine for a single-user mobile app.
    private static readonly SemaphoreSlim _ioLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IStorageRootService _storageRoot;

    public JsonTimelapseRepository(IStorageRootService storageRoot)
        => _storageRoot = storageRoot;

    private string ProjectsRoot => Path.Combine(_storageRoot.RootFolderPath, "projects");

    private string ProjectDir(Guid id) => Path.Combine(ProjectsRoot, id.ToString());

    private string ManifestPath(Guid id) => Path.Combine(ProjectDir(id), "project.json");

    public async Task<IReadOnlyList<TimelapseProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var root = ProjectsRoot;
        if (!Directory.Exists(root))
        {
            return [];
        }

        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            var projects = new List<TimelapseProject>();
            foreach (var dir in Directory.GetDirectories(root))
            {
                var manifest = Path.Combine(dir, "project.json");
                if (!File.Exists(manifest))
                {
                    continue;
                }

                var project = await ReadManifestAsync(manifest, cancellationToken);
                if (project is not null)
                {
                    projects.Add(project);
                }
            }
            return projects.OrderBy(p => p.CreatedAt).ToList();
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<TimelapseProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var manifest = ManifestPath(id);
        if (!File.Exists(manifest))
        {
            return null;
        }

        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadManifestAsync(manifest, cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveAsync(TimelapseProject project, CancellationToken cancellationToken = default)
    {
        var dir = ProjectDir(project.Id);
        Directory.CreateDirectory(dir);

        var dto = new ProjectDto
        {
            Id          = project.Id,
            Name        = project.Name,
            CreatedAt   = project.CreatedAt,
            ClonedFromId = project.ClonedFromId,
            Frames      = project.Frames.Select(f => new FrameDto
            {
                Id             = f.Id,
                Index          = f.Index,
                CapturedAt     = f.CapturedAt,
                FilePath       = f.FilePath,
                AlignedFilePath = f.AlignedFilePath
            }).ToList()
        };

        // Serialize into memory first so the file is never left in a partial
        // state and we hold the stream open for the shortest possible time.
        using var mem = new MemoryStream();
        await JsonSerializer.SerializeAsync(mem, dto, JsonOptions, cancellationToken);
        var bytes = mem.ToArray();

        var finalPath = ManifestPath(project.Id);
        var tempPath  = finalPath + ".tmp";

        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            // Write to a temp file then rename so readers never see a partial write.
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
            File.Move(tempPath, finalPath, overwrite: true);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dir = ProjectDir(id);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<TimelapseProject?> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<ProjectDto>(stream, JsonOptions, cancellationToken);
        if (dto is null)
        {
            return null;
        }

        var frames = dto.Frames?.Select(f => new TimelapseFrame(
            f.Id, f.Index, f.CapturedAt, f.FilePath, f.AlignedFilePath));

        return new TimelapseProject(dto.Id, dto.Name, dto.CreatedAt, dto.ClonedFromId, frames);
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class ProjectDto
    {
        public Guid             Id          { get; set; }
        public string           Name        { get; set; } = "";
        public DateTimeOffset   CreatedAt   { get; set; }
        public Guid?            ClonedFromId { get; set; }
        public List<FrameDto>?  Frames      { get; set; }
    }

    private sealed class FrameDto
    {
        public Guid           Id             { get; set; }
        public int            Index          { get; set; }
        public DateTimeOffset CapturedAt     { get; set; }
        public string         FilePath       { get; set; } = "";
        public string?        AlignedFilePath { get; set; }
    }
}
