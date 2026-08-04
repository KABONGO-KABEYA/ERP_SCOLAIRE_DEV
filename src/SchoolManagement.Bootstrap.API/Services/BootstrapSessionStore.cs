namespace SchoolManagement.Bootstrap.API.Services;

public sealed class BootstrapActivationSessionState
{
    public Guid BootstrapSessionId { get; init; }

    public Guid SchoolId { get; init; }

    public Guid SchoolActivationSessionId { get; init; }

    public Guid ActivationTokenId { get; init; }

    public string DeviceId { get; init; } = string.Empty;

    public DateTime ExpiresAtUtc { get; init; }

    public string Status { get; set; } = "pending";
}

public sealed class BootstrapSessionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, BootstrapActivationSessionState> _sessions = new();

    public BootstrapActivationSessionState Create(
        Guid schoolId,
        Guid schoolActivationSessionId,
        Guid activationTokenId,
        string deviceId,
        DateTime expiresAtUtc)
    {
        var state = new BootstrapActivationSessionState
        {
            BootstrapSessionId = Guid.NewGuid(),
            SchoolId = schoolId,
            SchoolActivationSessionId = schoolActivationSessionId,
            ActivationTokenId = activationTokenId,
            DeviceId = deviceId,
            ExpiresAtUtc = expiresAtUtc
        };

        lock (_gate)
        {
            _sessions[state.BootstrapSessionId] = state;
        }

        return state;
    }

    public BootstrapActivationSessionState Get(Guid bootstrapSessionId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(bootstrapSessionId, out var state))
            {
                throw new InvalidOperationException("Session Bootstrap introuvable.");
            }

            if (state.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _sessions.Remove(bootstrapSessionId);
                throw new InvalidOperationException("Session Bootstrap expirée.");
            }

            return state;
        }
    }

    public void MarkCompleted(Guid bootstrapSessionId)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(bootstrapSessionId, out var state))
            {
                state.Status = "completed";
            }
        }
    }
}
