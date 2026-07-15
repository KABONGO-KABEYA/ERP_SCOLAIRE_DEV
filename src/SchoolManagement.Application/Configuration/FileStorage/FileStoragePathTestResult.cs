namespace SchoolManagement.Application.Configuration.FileStorage;

public sealed class FileStoragePathTestResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public static FileStoragePathTestResult Success(string rootPath) =>
        new() { IsSuccess = true, Message = $"Accès au dossier confirmé : {rootPath}" };

    public static FileStoragePathTestResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}
