using System.Text.Json;
using System.Text.Json.Serialization;
using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Stores each project as a JSON sidecar next to its frame images.
///
/// Layout on device:
///   AppDataDirectory/
///     projects/
///       {projectId}/
///         project.json      ← metadata
///         frame_0.jpg
///         frame_1.jpg
///         ...
/// </summary>
public sealed class JsonTimelapseRepository : ITimelapseRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string ProjectsRoot => Path.Combine(FileSystem.AppDataDirectory, "projects");

    private string ProjectDir(Guid id) => Path.Combine(ProjectsRoot, id.ToString());

    private string ManifestPath(Guid id) => Path.Combine(ProjectDir(id), "project.json");

    public async Task<IReadOnlyList<TimelapseProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var root = ProjectsRoot;
        if (!Directory.Exists(root))
            return [];

        var projects = new List<TimelapseProject>();
        foreach (var dir in Directory.GetDirectories(root))
        {
            var manifest = Path.Combine(dir, "project.json");
            if (!File.Exists(manifest)) continue;

            var project = await ReadManifestAsync(manifest, cancellationToken);
            if (project is not null) projects.Add(project);
        }
        return projects.OrderBy(p => p.CreatedAt).ToList();
    }

    public async Task<TimelapseProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var manifest = ManifestPath(id);
        return File.Exists(manifest) ? await ReadManifestAsync(manifest, cancellationToken) : null;
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

        await using var stream = File.OpenWrite(ManifestPath(project.Id));
        stream.SetLength(0);
        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dir = ProjectDir(id);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<TimelapseProject?> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<ProjectDto>(stream, JsonOptions, cancellationToken);
        if (dto is null) return null;

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
