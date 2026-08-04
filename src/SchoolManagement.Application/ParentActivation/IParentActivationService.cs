namespace SchoolManagement.Application.ParentActivation;

public interface IParentActivationService
{
    Task<IssueParentActivationTokenResponse> IssueTokenAsync(
        Guid schoolId,
        Guid issuedByUserId,
        IssueParentActivationTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<ActivationSessionDto> StartAsync(
        ActivationStartRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolBindingDto> CompleteAsync(
        ActivationCompleteRequest request,
        CancellationToken cancellationToken = default);
}
