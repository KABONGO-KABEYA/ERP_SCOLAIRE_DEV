namespace SchoolManagement.Application.ParentActivation.BootstrapRelay;

public sealed record BootstrapRelayValidationResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    int? HttpStatusCode = null)
{
    public static BootstrapRelayValidationResult Ok() => new(true);

    public static BootstrapRelayValidationResult Fail(string message, int httpStatusCode) =>
        new(false, message, httpStatusCode);
}
