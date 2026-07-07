namespace SchoolManagement.Infrastructure.Services;



using Microsoft.Extensions.Configuration;

using SchoolManagement.Application.Common.Interfaces;



public sealed class LocalFileStorageService : IFileStorageService

{

    private readonly string _rootPath;



    public LocalFileStorageService(IConfiguration configuration)

    {

        _rootPath = Path.GetFullPath(configuration["FileStorage:UploadPath"] ?? "uploads");

        Directory.CreateDirectory(_rootPath);

    }



    public async Task<string> SaveAsync(

        Guid schoolId,

        Guid studentId,

        string fileName,

        Stream content,

        CancellationToken cancellationToken = default)

    {

        var safeName = Path.GetFileName(fileName);

        var relativeDir = Path.Combine(schoolId.ToString(), studentId.ToString());

        var absoluteDir = Path.Combine(_rootPath, relativeDir);

        Directory.CreateDirectory(absoluteDir);



        var uniqueName = $"{Guid.NewGuid():N}_{safeName}";

        var absolutePath = Path.Combine(absoluteDir, uniqueName);

        await using var fileStream = File.Create(absolutePath);

        await content.CopyToAsync(fileStream, cancellationToken);



        return Path.Combine(relativeDir, uniqueName).Replace('\\', '/');

    }



    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)

    {

        var absolutePath = ResolveSafePath(storagePath);

        if (absolutePath is null || !File.Exists(absolutePath))

        {

            return Task.FromResult<Stream?>(null);

        }



        return Task.FromResult<Stream?>(File.OpenRead(absolutePath));

    }



    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)

    {

        var absolutePath = ResolveSafePath(storagePath);

        if (absolutePath is not null && File.Exists(absolutePath))

        {

            File.Delete(absolutePath);

        }



        return Task.CompletedTask;

    }



    private string? ResolveSafePath(string storagePath)

    {

        if (string.IsNullOrWhiteSpace(storagePath))

        {

            return null;

        }



        var normalizedRoot = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

            + Path.DirectorySeparatorChar;

        var candidate = Path.GetFullPath(Path.Combine(_rootPath, storagePath.Replace('/', Path.DirectorySeparatorChar)));



        return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)

            ? candidate

            : null;

    }

}


