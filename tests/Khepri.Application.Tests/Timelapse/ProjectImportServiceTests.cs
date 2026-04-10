// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Text;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;
using NSubstitute;
using Shouldly;

namespace Khepri.Application.Tests.Timelapse;

public sealed class ProjectImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly IStorageRootService _storageRoot;
    private readonly ProjectImportService _sut;

    public ProjectImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);

        _storageRoot = Substitute.For<IStorageRootService>();
        _storageRoot.RootFolderPath.Returns(_root);

        _sut = new ProjectImportService(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Stream BuildZip(Action<ZipArchive> populate)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            populate(zip);
        }
        ms.Position = 0;
        return ms;
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_WhenNoProjectJson_ReturnsNull()
    {
        using var zip = BuildZip(z => AddEntry(z, "frame.jpg", "data"));

        var result = await _sut.ImportAsync(zip);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Import_WhenProjectJsonMissingId_ReturnsNull()
    {
        using var zip = BuildZip(z => AddEntry(z, "project.json", "{\"Name\":\"Test\"}"));

        var result = await _sut.ImportAsync(zip);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Import_WhenProjectJsonIdNotGuid_ReturnsNull()
    {
        using var zip = BuildZip(z => AddEntry(z, "project.json", "{\"Id\":\"not-a-guid\",\"Name\":\"Test\"}"));

        var result = await _sut.ImportAsync(zip);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Import_ValidZip_ReturnsProjectName()
    {
        var id = Guid.NewGuid();
        var json = $"{{\"Id\":\"{id}\",\"Name\":\"Sunrise\"}}";
        using var zip = BuildZip(z => AddEntry(z, "project.json", json));

        var result = await _sut.ImportAsync(zip);

        result.ShouldBe("Sunrise");
    }

    [Fact]
    public async Task Import_ValidZip_ExtractsFilesToProjectFolder()
    {
        var id = Guid.NewGuid();
        var json = $"{{\"Id\":\"{id}\",\"Name\":\"Sunrise\"}}";
        using var zip = BuildZip(z =>
        {
            AddEntry(z, "project.json", json);
            AddEntry(z, "frame1.jpg", "jpeg-data");
        });

        await _sut.ImportAsync(zip);

        var projectFolder = Path.Combine(_root, id.ToString());
        Directory.Exists(projectFolder).ShouldBeTrue();
        File.Exists(Path.Combine(projectFolder, "project.json")).ShouldBeTrue();
        File.Exists(Path.Combine(projectFolder, "frame1.jpg")).ShouldBeTrue();
    }

    [Fact]
    public async Task Import_WhenProjectAlreadyExists_OverwritesIt()
    {
        var id = Guid.NewGuid();
        var projectFolder = Path.Combine(_root, id.ToString());
        Directory.CreateDirectory(projectFolder);
        File.WriteAllText(Path.Combine(projectFolder, "old-file.txt"), "stale");

        var json = $"{{\"Id\":\"{id}\",\"Name\":\"Updated\"}}";
        using var zip = BuildZip(z => AddEntry(z, "project.json", json));

        var result = await _sut.ImportAsync(zip);

        result.ShouldBe("Updated");
        File.Exists(Path.Combine(projectFolder, "old-file.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(projectFolder, "project.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task Import_WhenNameMissing_ReturnsDefaultName()
    {
        var id = Guid.NewGuid();
        var json = $"{{\"Id\":\"{id}\"}}";
        using var zip = BuildZip(z => AddEntry(z, "project.json", json));

        var result = await _sut.ImportAsync(zip);

        result.ShouldBe("Imported Project");
    }
}
