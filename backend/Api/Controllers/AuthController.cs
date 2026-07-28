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
    private readonly IGraphGroupService _graphGroupService;
    private readonly HrGroupOptions _hrGroupOptions;

    public AuthController(IGraphGroupService graphGroupService, IOptions<HrGroupOptions> hrGroupOptions)
    {
        _graphGroupService = graphGroupService;
        _hrGroupOptions = hrGroupOptions.Value;
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeDto>> Me(CancellationToken ct)
    {
        var isHr = await _graphGroupService.IsCallerInGroupAsync(_hrGroupOptions.TrmRhAdmGroupId, ct);

        return Ok(new MeDto
        {
            ObjectId = User.GetObjectId(),
            DisplayName = User.GetDisplayName(),
            Email = User.GetEmail(),
            IsHr = isHr
        });
    }
}
