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

    public AuthController(IAdDirectoryService ad, IOptions<HrGroupOptions> hrGroupOptions)
    {
        _ad = ad;
        _hrGroupOptions = hrGroupOptions.Value;
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
}
