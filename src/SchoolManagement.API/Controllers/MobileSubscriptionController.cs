using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Abonnement portail parent mobile.
/// Les droits features pilotent le verrouillage UI — ne jamais hardcoder côté mobile.
/// </summary>
[ApiController]
[Authorize]
[Route("api/mobile")]
public class MobileSubscriptionController : ControllerBase
{
    [HttpGet("subscription")]
    [ProducesResponseType(typeof(ApiResponse<MobileSubscriptionDto>), StatusCodes.Status200OK)]
    public IActionResult GetSubscription()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        PremiumEntitlement? entitlement = null;
        var isPremium = Guid.TryParse(raw, out var userId)
            && MobileSubscriptionPaymentController.TryGetEntitlement(userId, out entitlement)
            && entitlement is not null
            && entitlement.ExpiresAt >= DateOnly.FromDateTime(DateTime.UtcNow);

        if (isPremium && entitlement is not null)
        {
            var expires = entitlement.ExpiresAt;
            return Ok(ApiResponse<MobileSubscriptionDto>.Ok(new MobileSubscriptionDto(
                IsPremium: true,
                ExpiryDate: expires,
                Plan: "Premium",
                Subscription: new MobileSubscriptionStatusDto(Active: true, ExpiresAt: expires),
                Features: MobileFeatureFlagsDto.Premium)));
        }

        return Ok(ApiResponse<MobileSubscriptionDto>.Ok(new MobileSubscriptionDto(
            IsPremium: false,
            ExpiryDate: null,
            Plan: "Free",
            Subscription: new MobileSubscriptionStatusDto(Active: false, ExpiresAt: null),
            Features: MobileFeatureFlagsDto.Free)));
    }
}

public sealed record MobileSubscriptionDto(
    bool IsPremium,
    DateOnly? ExpiryDate,
    string Plan,
    MobileSubscriptionStatusDto Subscription,
    MobileFeatureFlagsDto Features);

public sealed record MobileSubscriptionStatusDto(
    bool Active,
    DateOnly? ExpiresAt);

public sealed record MobileFeatureFlagsDto(
    bool Payments,
    bool Notes,
    bool Bulletins,
    bool Communications,
    bool Notifications,
    bool Attendance,
    bool Profile = true,
    bool SubscriptionManage = true)
{
    public static MobileFeatureFlagsDto Free { get; } = new(
        Payments: true,
        Notes: false,
        Bulletins: false,
        Communications: false,
        Notifications: false,
        Attendance: false,
        Profile: true,
        SubscriptionManage: true);

    public static MobileFeatureFlagsDto Premium { get; } = new(
        Payments: true,
        Notes: true,
        Bulletins: true,
        Communications: true,
        Notifications: true,
        Attendance: true,
        Profile: true,
        SubscriptionManage: true);
}
