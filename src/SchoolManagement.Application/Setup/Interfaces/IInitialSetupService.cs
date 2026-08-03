using SchoolManagement.Application.Setup.DTOs;

namespace SchoolManagement.Application.Setup.Interfaces;

public interface IInitialSetupService
{
    Task<InitialSetupStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<CompleteInitialSetupResultDto> CompleteAsync(
        CompleteInitialSetupRequest request,
        CancellationToken cancellationToken = default);
}
