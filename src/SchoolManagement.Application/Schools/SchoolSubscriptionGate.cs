using SchoolManagement.Application.Schools.DTOs;

namespace SchoolManagement.Application.Schools;

public enum SchoolSubscriptionGateDecision
{
    Allow,
    BlockExpired,
    BlockNotConfigured,
    BlockUnavailable
}

public sealed record SchoolSubscriptionGateResult(
    SchoolSubscriptionGateDecision Decision,
    SchoolSubscriptionDto? Subscription);

/// <summary>
/// Décision d'accès Desktop : seul un abonnement Valid ouvre le Shell.
/// Unavailable n'est jamais assimilé à Expired.
/// </summary>
public static class SchoolSubscriptionGate
{
    public static SchoolSubscriptionGateResult FromLookup(
        SchoolSubscriptionDto? subscription,
        bool isUnavailable)
    {
        if (isUnavailable)
        {
            return new SchoolSubscriptionGateResult(
                SchoolSubscriptionGateDecision.BlockUnavailable,
                subscription);
        }

        if (subscription is null
            || subscription.Status == SchoolSubscriptionStatuses.Unavailable)
        {
            return new SchoolSubscriptionGateResult(
                SchoolSubscriptionGateDecision.BlockUnavailable,
                subscription);
        }

        if (!subscription.IsConfigured
            || subscription.Status == SchoolSubscriptionStatuses.NotConfigured)
        {
            return new SchoolSubscriptionGateResult(
                SchoolSubscriptionGateDecision.BlockNotConfigured,
                subscription);
        }

        if (subscription.IsValid
            && subscription.Status == SchoolSubscriptionStatuses.Valid)
        {
            return new SchoolSubscriptionGateResult(
                SchoolSubscriptionGateDecision.Allow,
                subscription);
        }

        return new SchoolSubscriptionGateResult(
            SchoolSubscriptionGateDecision.BlockExpired,
            subscription);
    }
}
