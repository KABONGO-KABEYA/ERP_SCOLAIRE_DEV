using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Paiement abonnement Premium mobile (simulation prête pour Airtel/Orange/M-Pesa).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/mobile/subscription")]
public class MobileSubscriptionPaymentController : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, PremiumPaymentRecord> Payments = new();
    private static readonly ConcurrentDictionary<Guid, PremiumEntitlement> Entitlements = new();

    [HttpPost("payment/initiate")]
    [ProducesResponseType(typeof(ApiResponse<PremiumPaymentInitDto>), StatusCodes.Status200OK)]
    public IActionResult Initiate([FromBody] PremiumPaymentInitiateRequest request)
    {
        var userId = RequireUserId();
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.PaymentMethod) ||
            string.IsNullOrWhiteSpace(request.Plan))
        {
            return BadRequest(ApiResponse<object>.Fail("Requête de paiement invalide."));
        }

        var plan = request.Plan.Trim().ToLowerInvariant();
        var method = request.PaymentMethod.Trim().ToLowerInvariant();
        var (amount, currency, durationLabel, months) = ResolvePlan(plan);

        var id = Guid.NewGuid();
        var record = new PremiumPaymentRecord(
            Id: id,
            UserId: userId,
            Plan: plan,
            PaymentMethod: method,
            PhoneNumber: NormalizePhone(request.PhoneNumber),
            Amount: amount,
            Currency: currency,
            DurationLabel: durationLabel,
            Months: months,
            Status: "processing",
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            TransactionNumber: $"PRM-{DateTime.UtcNow:yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}");

        Payments[id] = record;
        return Ok(ApiResponse<PremiumPaymentInitDto>.Ok(ToInitDto(record)));
    }

    [HttpPost("payment/status")]
    [ProducesResponseType(typeof(ApiResponse<PremiumPaymentStatusDto>), StatusCodes.Status200OK)]
    public IActionResult Status([FromBody] PremiumPaymentStatusRequest request)
    {
        var userId = RequireUserId();
        if (!Payments.TryGetValue(request.PaymentId, out var record) || record.UserId != userId)
        {
            return NotFound(ApiResponse<object>.Fail("Paiement introuvable."));
        }

        if (record.Status is "pending" or "processing")
        {
            var elapsed = DateTime.UtcNow - record.CreatedAt;
            if (elapsed.TotalSeconds >= 4)
            {
                record = ActivateSuccess(record);
            }
        }

        return Ok(ApiResponse<PremiumPaymentStatusDto>.Ok(ToStatusDto(record)));
    }

    [HttpPost("payment/callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult Callback([FromBody] PremiumPaymentCallbackRequest request)
    {
        if (!Payments.TryGetValue(request.PaymentId, out var record))
        {
            return NotFound(ApiResponse<object>.Fail("Paiement introuvable."));
        }

        var status = (request.Status ?? "success").Trim().ToLowerInvariant();
        record = status switch
        {
            "success" or "succeeded" or "paid" => ActivateSuccess(record),
            "failed" or "refused" or "rejected" => record with
            {
                Status = "refused",
                UpdatedAt = DateTime.UtcNow,
                FailureReason = request.Message ?? "Paiement refusé par l'opérateur."
            },
            "expired" => record with { Status = "expired", UpdatedAt = DateTime.UtcNow },
            "cancelled" or "canceled" => record with { Status = "cancelled", UpdatedAt = DateTime.UtcNow },
            _ => record with { Status = "processing", UpdatedAt = DateTime.UtcNow }
        };

        Payments[record.Id] = record;
        return Ok(ApiResponse<object>.Ok(new { record.Id, record.Status }));
    }

    [HttpGet("payments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PremiumPaymentHistoryDto>>), StatusCodes.Status200OK)]
    public IActionResult History()
    {
        var userId = RequireUserId();
        var items = Payments.Values
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToHistoryDto)
            .ToList();
        return Ok(ApiResponse<IReadOnlyList<PremiumPaymentHistoryDto>>.Ok(items));
    }

    [HttpGet("payments/{paymentId:guid}/invoice/pdf")]
    public IActionResult InvoicePdf(Guid paymentId)
    {
        var userId = RequireUserId();
        if (!Payments.TryGetValue(paymentId, out var record) || record.UserId != userId)
        {
            return NotFound();
        }

        if (record.Status != "success")
        {
            return BadRequest(ApiResponse<object>.Fail("Facture disponible uniquement pour un paiement réussi."));
        }

        var bytes = BuildSimpleInvoicePdf(record);
        return File(bytes, "application/pdf", $"facture-{record.TransactionNumber}.pdf");
    }

    public static bool TryGetEntitlement(Guid userId, out PremiumEntitlement? entitlement) =>
        Entitlements.TryGetValue(userId, out entitlement);

    private PremiumPaymentRecord ActivateSuccess(PremiumPaymentRecord record)
    {
        var expires = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(record.Months));
        var updated = record with { Status = "success", UpdatedAt = DateTime.UtcNow };
        Payments[updated.Id] = updated;
        Entitlements[updated.UserId] = new PremiumEntitlement(updated.UserId, expires, updated.Plan);
        return updated;
    }

    private Guid RequireUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }

    private static (decimal Amount, string Currency, string DurationLabel, int Months) ResolvePlan(string plan) =>
        plan switch
        {
            "monthly" => (0.50m, "USD", "1 mois", 1),
            _ => (1.50m, "USD", "1 année scolaire", 12)
        };

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("243") && digits.Length >= 12)
        {
            digits = "0" + digits[3..];
        }

        return digits;
    }

    private static PremiumPaymentInitDto ToInitDto(PremiumPaymentRecord r) =>
        new(r.Id, r.TransactionNumber, r.Status, r.Amount, r.Currency, r.DurationLabel);

    private static PremiumPaymentStatusDto ToStatusDto(PremiumPaymentRecord r) =>
        new(r.Id, r.TransactionNumber, r.Status, r.Amount, r.Currency, r.DurationLabel,
            r.PaymentMethod, r.PhoneNumber, r.FailureReason, r.UpdatedAt);

    private static PremiumPaymentHistoryDto ToHistoryDto(PremiumPaymentRecord r) =>
        new(r.Id, r.TransactionNumber, r.CreatedAt, r.Amount, r.Currency, r.PaymentMethod,
            r.Status, r.PhoneNumber, r.DurationLabel, r.Status == "success");

    private static byte[] BuildSimpleInvoicePdf(PremiumPaymentRecord record)
    {
        var oneLine =
            $"Facture {record.TransactionNumber} - {record.Amount:0.00} {record.Currency} - {record.PaymentMethod} - {record.PhoneNumber}";
        oneLine = oneLine.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var streamContent = $"BT /F1 14 Tf 50 780 Td ({oneLine}) Tj ET\n";
        var streamObj =
            $"4 0 obj<< /Length {Encoding.ASCII.GetByteCount(streamContent)} >>stream\n{streamContent}endstream\nendobj\n";
        var objects = new List<string>
        {
            "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n",
            "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n",
            "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources<< /Font<< /F1 5 0 R >> >> >>endobj\n",
            streamObj,
            "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n"
        };

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append(obj);
        }

        var xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            sb.Append($"{offsets[i]:D10} 00000 n \n");
        }

        sb.Append($"trailer<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}

public sealed record PremiumPaymentInitiateRequest(
    string Plan,
    string PaymentMethod,
    string PhoneNumber);

public sealed record PremiumPaymentStatusRequest(Guid PaymentId);

public sealed record PremiumPaymentCallbackRequest(
    Guid PaymentId,
    string? Status,
    string? Message);

public sealed record PremiumPaymentInitDto(
    Guid PaymentId,
    string TransactionNumber,
    string Status,
    decimal Amount,
    string Currency,
    string DurationLabel);

public sealed record PremiumPaymentStatusDto(
    Guid PaymentId,
    string TransactionNumber,
    string Status,
    decimal Amount,
    string Currency,
    string DurationLabel,
    string PaymentMethod,
    string PhoneNumber,
    string? FailureReason,
    DateTime UpdatedAt);

public sealed record PremiumPaymentHistoryDto(
    Guid Id,
    string TransactionNumber,
    DateTime Date,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Status,
    string PhoneNumber,
    string DurationLabel,
    bool InvoiceAvailable);

public sealed record PremiumPaymentRecord(
    Guid Id,
    Guid UserId,
    string Plan,
    string PaymentMethod,
    string PhoneNumber,
    decimal Amount,
    string Currency,
    string DurationLabel,
    int Months,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string TransactionNumber,
    string? FailureReason = null);

public sealed record PremiumEntitlement(Guid UserId, DateOnly ExpiresAt, string Plan);
