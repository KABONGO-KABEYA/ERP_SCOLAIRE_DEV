namespace SchoolManagement.Application.Payments.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Constants;

/// <summary>
/// Politique de modification / annulation des versements déjà encaissés :
/// permissions granulaires, dernière journée encaissée,
/// et interdiction de toucher une tranche si des tranches suivantes sont déjà payées.
/// </summary>
public static class PaymentMutationPolicy
{
    public static void EnsureCanCancelPayment(ICurrentUserService currentUser) =>
        EnsureHasPermission(
            currentUser,
            Permissions.PaymentsCancel,
            "Seul un utilisateur autorisé peut annuler un frais déjà payé.");

    public static void EnsureCanUpdatePaymentNotes(ICurrentUserService currentUser) =>
        EnsureHasPermission(
            currentUser,
            Permissions.PaymentsNotesUpdate,
            "Seul un utilisateur autorisé peut modifier les notes d'un paiement.");

    public static void EnsureCanMutatePaidPayment(ICurrentUserService currentUser) =>
        EnsureCanMutatePaidPayment(
            currentUser,
            "Seul un utilisateur autorisé peut modifier ou supprimer un frais déjà payé.");

    public static void EnsureCanMutatePaidPayment(ICurrentUserService currentUser, string deniedMessage) =>
        EnsureHasPermission(currentUser, Permissions.PaymentsPaidMutation, deniedMessage);

    public static void EnsureCanAssignPricingCategory(ICurrentUserService currentUser, string deniedMessage) =>
        EnsureHasPermission(currentUser, Permissions.PricingCategoriesAssign, deniedMessage);

    private static void EnsureHasPermission(ICurrentUserService currentUser, string permissionCode, string deniedMessage)
    {
        if (!currentUser.HasPermission(permissionCode))
        {
            throw new DomainException(deniedMessage);
        }
    }

    /// <summary>
    /// Le paiement ciblé doit appartenir à la dernière journée encaissée du même type de frais.
    /// Les versements d'une même journée restent tous modifiables.
    /// </summary>
    public static void EnsureIsLatestCompletedPayment(
        Payment target,
        IReadOnlyList<Payment> studentYearPayments)
    {
        if (target.Status != PaymentStatus.Complet)
        {
            throw new DomainException("Seuls les paiements complets peuvent être modifiés ou annulés.");
        }

        var latest = OrderByMutationPriority(
                studentYearPayments.Where(p => p.Status == PaymentStatus.Complet))
            .FirstOrDefault();

        if (latest is not null && latest.PaymentDate.Date > target.PaymentDate.Date)
        {
            throw new DomainException(
                "Impossible : un encaissement à une journée postérieure existe déjà pour ce type de frais " +
                "(y compris pour un autre élève). " +
                "Modifiez ou annulez d'abord le versement à la date la plus récente.");
        }
    }

    public static bool IsLatestPaymentDay(DateTime paymentDate, DateTime? latestPaymentDate) =>
        latestPaymentDate is null || paymentDate.Date >= latestPaymentDate.Value.Date;

    public static void EnsureCreationDateAllowed(DateTime paymentDate, DateTime? latestPaymentDate)
    {
        if (!IsLatestPaymentDay(paymentDate, latestPaymentDate))
            throw new DomainException($"Impossible d’enregistrer ce paiement au {paymentDate:dd/MM/yyyy} : un encaissement du même type de frais existe au {latestPaymentDate:dd/MM/yyyy}. La date minimale autorisée est le {latestPaymentDate:dd/MM/yyyy} (même année scolaire, tous élèves confondus).");
    }

    /// <summary>Priorité rétrograde : date de paiement DESC, puis enregistrement DESC.</summary>
    public static IOrderedEnumerable<Payment> OrderByMutationPriority(IEnumerable<Payment> payments) =>
        payments
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id);

    /// <summary>
    /// Interdit de modifier / annuler un versement qui touche une tranche
    /// si des tranches suivantes (SortOrder plus élevé) ont déjà des paiements.
    /// </summary>
    public static void EnsureNoLaterInstallmentsPaid(
        IReadOnlyList<PaymentLine> targetLines,
        IReadOnlyList<(Guid InstallmentId, int SortOrder)> installmentOrders,
        IReadOnlyDictionary<Guid, decimal> paidByInstallmentId)
    {
        var touchedIds = targetLines
            .Where(l => l.FeeInstallmentId.HasValue)
            .Select(l => l.FeeInstallmentId!.Value)
            .Distinct()
            .ToHashSet();

        if (touchedIds.Count == 0)
        {
            return;
        }

        var orderMap = installmentOrders.ToDictionary(x => x.InstallmentId, x => x.SortOrder);
        var maxTouchedOrder = touchedIds
            .Select(id => orderMap.GetValueOrDefault(id, int.MaxValue))
            .DefaultIfEmpty(int.MaxValue)
            .Max();

        var laterPaid = installmentOrders
            .Where(i => i.SortOrder > maxTouchedOrder)
            .Where(i => paidByInstallmentId.GetValueOrDefault(i.InstallmentId) > 0)
            .Select(i => i.InstallmentId)
            .ToList();

        if (laterPaid.Count > 0)
        {
            throw new DomainException(
                "Impossible de modifier ou supprimer ce versement : des tranches suivantes sont déjà payées. " +
                "Procédez d'abord sur le dernier versement / la dernière tranche payée (ordre rétrograde).");
        }
    }

    /// <summary>
    /// Pour un changement de montant tarifaire d'une tranche : interdit si des tranches
    /// suivantes ont déjà un montant payé (soldes élèves).
    /// </summary>
    public static void EnsureScheduleInstallmentEditable(
        int targetSortOrder,
        IReadOnlyList<(int SortOrder, decimal AmountPaid)> siblingInstallmentPayments)
    {
        var laterPaid = siblingInstallmentPayments
            .Any(x => x.SortOrder > targetSortOrder && x.AmountPaid > 0);

        if (laterPaid)
        {
            throw new DomainException(
                "Impossible de modifier le montant de cette tranche : des tranches suivantes sont déjà payées. " +
                "Modifiez d'abord les tranches les plus récentes (ordre rétrograde).");
        }
    }
}
