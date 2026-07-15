namespace SchoolManagement.Application.Configuration.FileStorage;

public sealed class FileStorageConfigurationValidationResult
{
    public bool IsValid => FieldErrors.Count == 0;

    public Dictionary<string, string> FieldErrors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddError(string fieldName, string message) => FieldErrors[fieldName] = message;
}
