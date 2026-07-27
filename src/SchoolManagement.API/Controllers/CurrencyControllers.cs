using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.CurrencyManagement.DTOs;
using SchoolManagement.Application.CurrencyManagement.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.Currencies)]
public sealed class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _service;
    private readonly ICurrentUserService _currentUser;

    public CurrenciesController(ICurrencyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.CurrenciesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CurrencyDefinitionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        var items = await _service.SearchCurrenciesAsync(search, activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CurrencyDefinitionDto>>.Ok(items));
    }

    [HttpGet("main")]
    [Authorize(Policy = Permissions.CurrenciesRead)]
    public async Task<IActionResult> GetMain(CancellationToken cancellationToken)
    {
        var item = await _service.GetMainCurrencyAsync(RequireSchoolId(), cancellationToken);
        return Ok(ApiResponse<CurrencyDefinitionDto>.Ok(item));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.CurrenciesCreate)]
    public async Task<IActionResult> Create([FromBody] SaveCurrencyDefinitionRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.CreateCurrencyAsync(request, UserId(), cancellationToken);
        return Ok(ApiResponse<CurrencyDefinitionDto>.Ok(item, "Devise créée."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.CurrenciesUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCurrencyDefinitionRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.UpdateCurrencyAsync(id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<CurrencyDefinitionDto>.Ok(item, "Devise mise à jour."));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Permissions.CurrenciesUpdate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await _service.SetCurrencyActiveAsync(id, true, UserId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Devise activée."));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.CurrenciesUpdate)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _service.SetCurrencyActiveAsync(id, false, UserId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Devise désactivée."));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();

    private Guid UserId() =>
        _currentUser.UserId ?? Guid.Empty;
}

[ApiController]
[Authorize]
[Route(ApiRoutes.SchoolCurrencies)]
public sealed class SchoolCurrenciesController : ControllerBase
{
    private readonly ICurrencyService _service;
    private readonly ICurrentUserService _currentUser;

    public SchoolCurrenciesController(ICurrencyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.CurrenciesRead)]
    public async Task<IActionResult> List([FromQuery] bool paymentOnly = false, CancellationToken cancellationToken = default)
    {
        var items = await _service.GetAllowedCurrenciesAsync(RequireSchoolId(), paymentOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SchoolCurrencyDto>>.Ok(items));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.CurrenciesUpdate)]
    public async Task<IActionResult> Upsert([FromBody] SaveSchoolCurrencyRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.UpsertSchoolCurrencyAsync(RequireSchoolId(), request, UserId(), cancellationToken);
        return Ok(ApiResponse<SchoolCurrencyDto>.Ok(item, "Devise d'établissement enregistrée."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.CurrenciesDelete)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        await _service.RemoveSchoolCurrencyAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Devise d'établissement retirée."));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();

    private Guid UserId() =>
        _currentUser.UserId ?? Guid.Empty;
}

[ApiController]
[Authorize]
[Route(ApiRoutes.ExchangeRateTypes)]
public sealed class ExchangeRateTypesController : ControllerBase
{
    private readonly ICurrencyService _service;
    private readonly ICurrentUserService _currentUser;

    public ExchangeRateTypesController(ICurrencyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ExchangeRatesRead)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var items = await _service.GetRateTypesAsync(activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ExchangeRateTypeDto>>.Ok(items));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ExchangeRatesCreate)]
    public async Task<IActionResult> Create([FromBody] SaveExchangeRateTypeRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.CreateRateTypeAsync(request, UserId(), cancellationToken);
        return Ok(ApiResponse<ExchangeRateTypeDto>.Ok(item, "Type de taux créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ExchangeRatesUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveExchangeRateTypeRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.UpdateRateTypeAsync(id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<ExchangeRateTypeDto>.Ok(item, "Type de taux mis à jour."));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Permissions.ExchangeRatesUpdate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await _service.SetRateTypeActiveAsync(id, true, UserId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Type de taux activé."));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.ExchangeRatesUpdate)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _service.SetRateTypeActiveAsync(id, false, UserId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Type de taux désactivé."));
    }

    private Guid UserId() =>
        _currentUser.UserId ?? Guid.Empty;
}

[ApiController]
[Authorize]
[Route(ApiRoutes.ExchangeRates)]
public sealed class ExchangeRatesController : ControllerBase
{
    private readonly ICurrencyService _service;
    private readonly ICurrentUserService _currentUser;

    public ExchangeRatesController(ICurrencyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ExchangeRatesRead)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? sourceCurrencyId,
        [FromQuery] Guid? targetCurrencyId,
        [FromQuery] Guid? rateTypeId,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        var items = await _service.SearchExchangeRatesAsync(
            sourceCurrencyId, targetCurrencyId, rateTypeId, activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ExchangeRateDto>>.Ok(items));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.ExchangeRatesRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetRateByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ExchangeRateDto>.Ok(item));
    }

    [HttpGet("active")]
    [Authorize(Policy = Permissions.ExchangeRatesRead)]
    public async Task<IActionResult> GetActive(
        [FromQuery] Guid sourceCurrencyId,
        [FromQuery] Guid targetCurrencyId,
        [FromQuery] Guid? rateTypeId,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetActiveExchangeRateAsync(sourceCurrencyId, targetCurrencyId, rateTypeId, cancellationToken);
        return Ok(ApiResponse<ExchangeRateDto?>.Ok(item));
    }

    [HttpGet("for-date")]
    [Authorize(Policy = Permissions.ExchangeRatesRead)]
    public async Task<IActionResult> GetForDate(
        [FromQuery] Guid sourceCurrencyId,
        [FromQuery] Guid targetCurrencyId,
        [FromQuery] DateOnly asOfDate,
        [FromQuery] Guid? rateTypeId,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetRateForDateAsync(sourceCurrencyId, targetCurrencyId, asOfDate, rateTypeId, cancellationToken);
        return Ok(ApiResponse<ExchangeRateDto?>.Ok(item));
    }

    [HttpPost("convert")]
    [Authorize(Policy = Permissions.ExchangeRatesRead)]
    public async Task<IActionResult> Convert([FromBody] CurrencyConversionRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.ConvertAsync(request, cancellationToken);
        return Ok(ApiResponse<CurrencyConversionResultDto>.Ok(item));
    }

    [HttpGet("history")]
    [Authorize(Policy = Permissions.ExchangeRateHistoryRead)]
    public async Task<IActionResult> History(
        [FromQuery] Guid? exchangeRateId,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        var items = await _service.GetExchangeRateHistoryAsync(exchangeRateId, take, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ExchangeRateHistoryDto>>.Ok(items));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ExchangeRatesCreate)]
    public async Task<IActionResult> Create([FromBody] SaveExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.CreateExchangeRateAsync(
            request, UserId(), Environment.MachineName, ClientIp(), cancellationToken);
        return Ok(ApiResponse<ExchangeRateDto>.Ok(item, "Taux de change créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ExchangeRatesUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveExchangeRateRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.UpdateExchangeRateAsync(
            id, request, UserId(), Environment.MachineName, ClientIp(), cancellationToken);
        return Ok(ApiResponse<ExchangeRateDto>.Ok(item, "Taux de change mis à jour."));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Permissions.ExchangeRatesActivate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await _service.ActivateExchangeRateAsync(id, UserId(), Environment.MachineName, ClientIp(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Taux activé."));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.ExchangeRatesDelete)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _service.SoftDeleteExchangeRateAsync(id, UserId(), Environment.MachineName, ClientIp(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Taux désactivé."));
    }

    private Guid UserId() =>
        _currentUser.UserId ?? Guid.Empty;

    private string? ClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();
}
