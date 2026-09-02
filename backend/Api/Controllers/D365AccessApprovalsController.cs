using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Models.Dtos;
using TremblantLifecycle.Api.Models.Entities;
using TremblantLifecycle.Api.Services;

namespace TremblantLifecycle.Api.Controllers;

/// <summary>The approver-facing screen: every pending/completed D365 access approval, the
/// prepopulated French form for one of them (with a peer-roles comparison list), and the action
/// that actually creates the TDX ticket. Separate from AdminRequestsController on purpose — the
/// authorization model here is "matched D365Approver", not "AppUsers row" (see D365Approver's doc
/// comment); an AppUsers Admin gets read-only oversight but not the Envoyer action unless they are
/// ALSO a matched approver.</summary>
[ApiController]
[Route("api/d365-access-approvals")]
[Authorize]
public class D365AccessApprovalsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;
    private readonly ID365ApproverService _approvers;
    private readonly IAppUserService _appUsers;
    private readonly IAdDirectoryService _ad;
    private readonly ITicketOrchestrationService _orchestration;
    private readonly ITicketStatusService _statuses;
    private readonly ILogger<D365AccessApprovalsController> _logger;

    public D365AccessApprovalsController(
        AppDbContext db,
        WorkdayContext workday,
        ID365ApproverService approvers,
        IAppUserService appUsers,
        IAdDirectoryService ad,
        ITicketOrchestrationService orchestration,
        ITicketStatusService statuses,
        ILogger<D365AccessApprovalsController> logger)
    {
        _db = db;
        _workday = workday;
        _approvers = approvers;
        _appUsers = appUsers;
        _ad = ad;
        _orchestration = orchestration;
        _statuses = statuses;
        _logger = logger;
    }

    /// <summary>Any D365Approver access at all, OR any Administration access (Lecteur/Admin) for
    /// oversight — enough to LIST and VIEW, not to complete one.</summary>
    private async Task<bool> CanViewAsync(CancellationToken ct) =>
        await _approvers.HasAnyAccessAsync(User.GetObjectId(), ct) || await _appUsers.HasAnyAccessAsync(User.GetObjectId(), ct);

    [HttpGet]
    public async Task<ActionResult<List<D365AccessApprovalSummaryDto>>> List(CancellationToken ct)
    {
        if (!await CanViewAsync(ct)) return Forbid();

        var approvals = await _db.D365AccessApprovals.AsNoTracking()
            .Include(a => a.Request)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var employeeNames = await _db.RequestEmployees.AsNoTracking()
            .Where(e => approvals.Select(a => a.RequestEmployeeId).Contains(e.RequestEmployeeId))
            .ToDictionaryAsync(e => e.RequestEmployeeId, e => e.NameSnapshot, ct);

        var employeeWorkdayIds = await _db.RequestEmployees.AsNoTracking()
            .Where(e => approvals.Select(a => a.RequestEmployeeId).Contains(e.RequestEmployeeId))
            .ToDictionaryAsync(e => e.RequestEmployeeId, e => e.WorkdayEmployeeId, ct);

        var workdayIds = employeeWorkdayIds.Values.Distinct().ToList();
        var positionTitles = await _workday.WorkdayDemographics
            .Where(w => w.PrimaryJob == true && workdayIds.Contains(w.EmployeeId))
            .Select(w => new { w.EmployeeId, w.PositionTitle })
            .ToDictionaryAsync(w => w.EmployeeId, w => w.PositionTitle, ct);

        var requestIds = approvals.Select(a => a.RequestId).ToList();
        var d365Tickets = await _db.RequestTickets.AsNoTracking()
            .Where(t => requestIds.Contains(t.RequestId) && t.Kind == TicketKind.D365Access)
            .ToListAsync(ct);
        var ticketByRequest = d365Tickets.ToDictionary(t => t.RequestId);
        var liveStatuses = await _statuses.GetStatusesAsync(d365Tickets, ct);

        var items = approvals.Select(a =>
        {
            ticketByRequest.TryGetValue(a.RequestId, out var ticket);
            var live = ticket is not null && liveStatuses.TryGetValue(ticket.RequestTicketId, out var s) ? s : null;
            var workdayId = employeeWorkdayIds.GetValueOrDefault(a.RequestEmployeeId);

            return new D365AccessApprovalSummaryDto
            {
                RequestId = a.RequestId,
                RequestNumber = a.Request.RequestNumber,
                EmployeeName = employeeNames.GetValueOrDefault(a.RequestEmployeeId, "?"),
                PositionTitle = workdayId is not null ? positionTitles.GetValueOrDefault(workdayId) : null,
                Status = a.Status.ToString(),
                CreatedAt = a.CreatedAt,
                CompletedAt = a.CompletedAt,
                CompletedByDisplayName = a.CompletedByDisplayName,
                TicketNumber = ticket?.Outcome == TicketOutcome.Created ? ticket.TicketNumber : null,
                TicketState = ticket?.Outcome == TicketOutcome.Failed ? "Failed" : live?.State.ToString(),
                TicketStateLabel = ticket?.Outcome == TicketOutcome.Failed ? "Échec de création" : live?.Label
            };
        }).ToList();

        return Ok(items);
    }

    [HttpGet("{requestId:int}")]
    public async Task<ActionResult<D365AccessApprovalDetailDto>> Detail(int requestId, CancellationToken ct)
    {
        var approval = await _db.D365AccessApprovals.AsNoTracking()
            .Include(a => a.Roles)
            .Include(a => a.Request).ThenInclude(r => r.OnboardingDetail)
            .FirstOrDefaultAsync(a => a.RequestId == requestId, ct);
        if (approval is null) return NotFound();

        var employee = await _db.RequestEmployees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.RequestEmployeeId == approval.RequestEmployeeId, ct);
        if (employee is null) return NotFound();

        var workdayInfo = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.PrimaryJob == true)
            .Select(w => new { w.JobCode, w.PositionTitle, w.WorkEmail, w.Email, w.ManagerId, w.Manager })
            .FirstOrDefaultAsync(ct);

        if (!await CanViewAsync(ct)) return Forbid();
        var canComplete = await _approvers.CanActOnAsync(User.GetObjectId(), workdayInfo?.PositionTitle, ct);

        string? managerName = workdayInfo?.Manager;
        if (!string.IsNullOrWhiteSpace(workdayInfo?.ManagerId))
        {
            var manager = await _workday.WorkdayDemographics
                .Where(w => w.EmployeeId == workdayInfo.ManagerId && w.PrimaryJob == true)
                .Select(w => new { w.FirstName, w.PreferredFirstName, w.LastName })
                .FirstOrDefaultAsync(ct);
            if (manager is not null)
            {
                managerName = $"{manager.PreferredFirstName ?? manager.FirstName} {manager.LastName}";
            }
        }

        var peers = await BuildPeersAsync(employee.WorkdayEmployeeId, workdayInfo?.JobCode, workdayInfo?.PositionTitle, ct);

        return Ok(new D365AccessApprovalDetailDto
        {
            RequestId = approval.RequestId,
            RequestNumber = approval.Request.RequestNumber,
            Status = approval.Status.ToString(),
            CanComplete = canComplete && approval.Status == D365ApprovalStatus.Pending,
            RequesterName = approval.Request.CreatedByDisplayName,
            EmployeeName = employee.NameSnapshot,
            EmployeeEmail = workdayInfo?.WorkEmail ?? workdayInfo?.Email,
            ManagerName = managerName,
            PositionTitle = workdayInfo?.PositionTitle ?? employee.PositionSnapshot,
            JobCode = workdayInfo?.JobCode ?? employee.CodeEmploiSnapshot,
            Departement = employee.DepartementSnapshot,
            StartDate = approval.Request.OnboardingDetail?.DateEntreePrevue,
            JobTitleEnglish = approval.JobTitleEnglish ?? workdayInfo?.PositionTitle ?? employee.PositionSnapshot,
            LegalEntity = approval.LegalEntity,
            DepartmentNumber = approval.DepartmentNumber,
            ApprovalLimit = approval.ApprovalLimit,
            ApAccessDetails = approval.ApAccessDetails,
            AdditionalLegalEntities = approval.AdditionalLegalEntities,
            LevyEmployee = approval.LevyEmployee,
            Roles = approval.Roles.Select(r => r.Role).OrderBy(r => r).ToList(),
            RoleCatalog = TdxD365RoleCheckboxes.All.ToList(),
            Peers = peers
        });
    }

    /// <summary>Same Job Code AND Position Title, excluding this employee, and the D365 security
    /// roles each already holds (D365UserSecurityRole, joined by Workday EmployeeId — see
    /// DiscrepanciesController for the same join pattern). Peers with roles sort first: they're the
    /// useful reference signal.</summary>
    private async Task<List<D365PeerRoleDto>> BuildPeersAsync(string employeeWorkdayId, string? jobCode, string? positionTitle, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(jobCode) || string.IsNullOrWhiteSpace(positionTitle)) return [];

        var peers = await _workday.WorkdayDemographics
            .Where(w => w.PrimaryJob == true && w.JobCode == jobCode && w.PositionTitle == positionTitle && w.EmployeeId != employeeWorkdayId)
            .Select(w => new { w.EmployeeId, w.FirstName, w.PreferredFirstName, w.LastName })
            .ToListAsync(ct);
        if (peers.Count == 0) return [];

        var peerIds = peers.Select(p => p.EmployeeId).ToList();
        var peerRoles = await _db.D365UserSecurityRoles.AsNoTracking()
            .Where(r => r.EmployeeId != null && peerIds.Contains(r.EmployeeId))
            .ToListAsync(ct);
        var rolesByEmployee = peerRoles
            .GroupBy(r => r.EmployeeId!)
            .ToDictionary(g => g.Key, g => g.Select(r => r.SecurityRole).Distinct().OrderBy(r => r).ToList());

        return peers
            .Select(p => new D365PeerRoleDto
            {
                EmployeeName = $"{p.PreferredFirstName ?? p.FirstName} {p.LastName}",
                EmployeeId = p.EmployeeId,
                Roles = rolesByEmployee.GetValueOrDefault(p.EmployeeId, [])
            })
            .OrderByDescending(p => p.Roles.Count)
            .ThenBy(p => p.EmployeeName)
            .ToList();
    }

    /// <summary>The Envoyer action. Saves what the approver entered, then immediately attempts the
    /// real TDX ticket — the approval is marked Completed either way (the human decision is made;
    /// whether the downstream TDX call itself succeeded is reported back and, if not, handled by the
    /// normal Administration/Réessayer path like every other ticket kind).</summary>
    [HttpPost("{requestId:int}/complete")]
    public async Task<ActionResult<CompleteD365AccessApprovalResultDto>> Complete(int requestId, CompleteD365AccessApprovalDto dto, CancellationToken ct)
    {
        var approval = await _db.D365AccessApprovals
            .Include(a => a.Roles)
            .Include(a => a.Request).ThenInclude(r => r.Employees)
            .Include(a => a.Request).ThenInclude(r => r.OnboardingDetail)
            .FirstOrDefaultAsync(a => a.RequestId == requestId, ct);
        if (approval is null) return NotFound();

        if (approval.Status != D365ApprovalStatus.Pending)
        {
            return Conflict(new CompleteD365AccessApprovalResultDto { Succeeded = false, Error = "Cette approbation a déjà été complétée." });
        }

        var employee = approval.Request.Employees.FirstOrDefault(e => e.RequestEmployeeId == approval.RequestEmployeeId);
        var positionTitle = employee is null ? null : await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.PrimaryJob == true)
            .Select(w => w.PositionTitle)
            .FirstOrDefaultAsync(ct);

        if (!await _approvers.CanActOnAsync(User.GetObjectId(), positionTitle, ct)) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.JobTitleEnglish) || string.IsNullOrWhiteSpace(dto.LegalEntity) || string.IsNullOrWhiteSpace(dto.DepartmentNumber))
        {
            return BadRequest("Le titre du poste (anglais), l'entité légale et le numéro de département sont requis.");
        }
        if (dto.ApprovalLimit < 0)
        {
            return BadRequest("La limite d'approbation ne peut pas être négative.");
        }
        var invalidRoles = dto.Roles.Except(TdxD365RoleCheckboxes.All).ToList();
        if (invalidRoles.Count > 0)
        {
            return BadRequest($"Rôle(s) inconnu(s) : {string.Join(", ", invalidRoles)}");
        }

        approval.JobTitleEnglish = dto.JobTitleEnglish.Trim();
        approval.LegalEntity = dto.LegalEntity.Trim();
        approval.DepartmentNumber = dto.DepartmentNumber.Trim();
        approval.ApprovalLimit = dto.ApprovalLimit;
        approval.LevyEmployee = dto.LevyEmployee;
        approval.ApAccessDetails = string.IsNullOrWhiteSpace(dto.ApAccessDetails) ? null : dto.ApAccessDetails.Trim();
        approval.AdditionalLegalEntities = string.IsNullOrWhiteSpace(dto.AdditionalLegalEntities) ? null : dto.AdditionalLegalEntities.Trim();
        approval.Roles.Clear();
        foreach (var role in dto.Roles.Distinct())
        {
            approval.Roles.Add(new D365AccessApprovalRole { Role = role });
        }
        approval.Status = D365ApprovalStatus.Completed;
        approval.CompletedByObjectId = User.GetObjectId();
        approval.CompletedByDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        approval.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("D365 approver {Approver} completed approval for request {RequestNumber}", User.GetObjectId(), approval.Request.RequestNumber);

        var result = await _orchestration.CreateD365AccessTicketAsync(approval.Request, approval, ct);

        return Ok(new CompleteD365AccessApprovalResultDto
        {
            Succeeded = result.Succeeded,
            TicketNumber = result.TicketNumber,
            Error = result.Error
        });
    }
}
