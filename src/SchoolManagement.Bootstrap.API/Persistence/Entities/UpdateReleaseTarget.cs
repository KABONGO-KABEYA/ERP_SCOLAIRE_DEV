namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

public sealed class UpdateReleaseTarget
{
    public Guid TargetId { get; set; } = Guid.NewGuid();

    public Guid ReleaseId { get; set; }

    /// <summary>
    /// <c>null</c> = toutes les écoles du channel. Ce n'est pas une preuve d'identité.
    /// </summary>
    public Guid? SchoolId { get; set; }

    public UpdateRelease Release { get; set; } = null!;

    public BootstrapSchoolRegistryEntry? School { get; set; }
}
