namespace SchoolManagement.Application.Parent.Interfaces;

using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Domain.Entities.Students;

public interface IParentAccessProvisioningService
{
    /// <summary>
    /// Crée (si besoin) un compte UserAccount rôle PARENT lié à chaque tuteur,
    /// pour l'accès à l'application mobile.
    /// </summary>
    Task<IReadOnlyList<ParentAppAccessCredentialDto>> EnsureAccessForGuardiansAsync(
        Guid schoolId,
        IReadOnlyList<Guardian> guardians,
        CancellationToken cancellationToken = default);
}
