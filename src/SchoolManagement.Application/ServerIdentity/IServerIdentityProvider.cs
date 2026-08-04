namespace SchoolManagement.Application.ServerIdentity;

public interface IServerIdentityProvider
{
    ServerIdentitySnapshot Current { get; }

    /// <summary>Recharge le snapshot (école, licence) après setup ou changement config.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
