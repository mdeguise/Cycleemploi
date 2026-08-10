using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAdDirectoryService _ad;
    private readonly HrGroupOptions _hrGroupOptions;
    private readonly ITdxService _tdx;
    private readonly TdxOptions _tdxOptions;

    public AuthController(IAdDirectoryService ad, IOptions<HrGroupOptions> hrGroupOptions, ITdxService tdx, IOptions<TdxOptions> tdxOptions)
    {
        _ad = ad;
        _hrGroupOptions = hrGroupOptions.Value;
        _tdx = tdx;
        _tdxOptions = tdxOptions.Value;
    }

    [HttpGet("me")]
    public ActionResult<MeDto> Me()
    {
        var accountName = User.GetObjectId();
        var sam = User.GetSamAccountName();
        var info = _ad.GetUserInfo(sam);
        var isHr = _ad.IsUserInGroup(sam, _hrGroupOptions.TrmRhAdmGroupName);

        return Ok(new MeDto
        {
            ObjectId = accountName,
            DisplayName = info.DisplayName ?? accountName,
            Email = info.Email,
            IsHr = isHr
        });
    }

    /// <summary>Builds the "Besoin d'aide?" link to the TDX client-portal Incident form, with the
    /// current user's TDX UID attached as the userId param so the form opens pre-associated with
    /// them as requester. The UID lookup is best-effort: if it fails for any reason, the URL is
    /// still returned, just without userId — same as the form behaves for someone typing the base
    /// URL by hand.</summary>
    [HttpGet("help-url")]
    public async Task<ActionResult<HelpUrlDto>> HelpUrl(CancellationToken ct)
    {
        var sam = User.GetSamAccountName();
        var info = _ad.GetUserInfo(sam);
        var uid = string.IsNullOrWhiteSpace(info.Email) ? null : await _tdx.TryLookupPersonUidAsync(info.Email, ct);

        var query = $"resortId={_tdxOptions.HelpFormResortId}" +
            $"&categoryId={_tdxOptions.HelpFormCategoryId}" +
            $"&serviceId={_tdxOptions.HelpFormServiceId}" +
            $"&offeringId={_tdxOptions.HelpFormOfferingId}" +
            $"&__cust={Uri.EscapeDataString(_tdxOptions.HelpFormCustomer)}" +
            $"&i={Uri.EscapeDataString(_tdxOptions.HelpFormItemId)}";
        if (!string.IsNullOrWhiteSpace(uid))
        {
            query += $"&userId={Uri.EscapeDataString(uid)}";
        }

        return Ok(new HelpUrlDto { Url = $"{_tdxOptions.HelpFormBaseUrl}?{query}" });
    }

    /// <summary>Creates a real TDX ticket from the in-app French "Besoin d'aide?" form — unlike the
    /// background integrations elsewhere in this app, this is a direct user action awaiting a
    /// result, so failures are surfaced to the caller rather than swallowed/emailed.</summary>
    [HttpPost("help-ticket")]
    public async Task<ActionResult<HelpTicketResultDto>> CreateHelpTicket([FromBody] CreateHelpTicketDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            return BadRequest("La description est requise.");
        }

        var sam = User.GetSamAccountName();
        var info = _ad.GetUserInfo(sam);
        if (string.IsNullOrWhiteSpace(info.Email))
        {
            return Problem("Impossible de déterminer votre adresse courriel.", statusCode: StatusCodes.Status500InternalServerError);
        }

        try
        {
            var ticketId = await _tdx.CreateHelpTicketAsync(info.DisplayName ?? sam, info.Email, dto.Description, ct);
            return Ok(new HelpTicketResultDto { TicketId = ticketId });
        }
        catch (TdxTicketException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
