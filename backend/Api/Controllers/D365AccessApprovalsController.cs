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
    /// <summary>Every D365 access request uses the same D365 F&amp;O legal entity — set here, never
    /// an approver input. Internal so RequestsController/TicketOrchestrationService can stamp it on
    /// wizard-driven approvals too, without a second literal "6201" to drift out of sync.</summary>
    internal const string FixedLegalEntity = "6201";

    /// <summary>The real TDX form's own wording for its "Access Type" dropdown — matched exactly
    /// (see D365AccessApproval.AccessType) so the value can be sent straight through. Internal so
    /// RequestsController can validate the wizard's own "D365 et Dynaway" step against the same
    /// catalog.</summary>
    internal static readonly string[] AllowedAccessTypes = ["New Access", "Change Access", "Remove Access"];

    /// <summary>The ad-hoc form's "Limite d'approbation" catalog — everyone gets StandardApprovalLimits;
    /// this short allowlist (case-insensitive email match) gets ElevatedApprovalLimits instead, up to
    /// the real TDX form's own highest tier. Re-validated here even though the frontend already only
    /// shows the matching dropdown — a client can submit whatever it wants, so this is the actual
    /// control, not the dropdown. Internal so RequestsController can apply the same validation to the
    /// wizard's own "D365 et Dynaway" step.</summary>
    internal static readonly string[] ElevatedApprovalLimitEmails = ["mbessette@tremblant.ca"];
    internal static readonly decimal[] StandardApprovalLimits = [0, 2000, 5000];
    internal static readonly decimal[] ElevatedApprovalLimits = [0, 2000, 5000, 25000, 50000, 100000, 500000, 1000000, 1500000];

    private readonly AppDbContext _db;
    private readonly WorkdayContext _workday;
    private readonly ID365ApproverService _approvers;
    private readonly ID365ViewerService _viewers;
    private readonly IAppUserService _appUsers;
    private readonly IAdDirectoryService _ad;
    private readonly ITicketOrchestrationService _orchestration;
    private readonly ITicketStatusService _statuses;
    private readonly RequestNumberService _requestNumbers;
    private readonly ILogger<D365AccessApprovalsController> _logger;

    public D365AccessApprovalsController(
        AppDbContext db,
        WorkdayContext workday,
        ID365ApproverService approvers,
        ID365ViewerService viewers,
        IAppUserService appUsers,
        IAdDirectoryService ad,
        ITicketOrchestrationService orchestration,
        ITicketStatusService statuses,
        RequestNumberService requestNumbers,
        ILogger<D365AccessApprovalsController> logger)
    {
        _db = db;
        _workday = workday;
        _approvers = approvers;
        _viewers = viewers;
        _appUsers = appUsers;
        _ad = ad;
        _orchestration = orchestration;
        _statuses = statuses;
        _requestNumbers = requestNumbers;
        _logger = logger;
    }

    /// <summary>D365Approver or D365Viewer access, OR AppUsers Admin as a safety net (so an
    /// administrator never loses the ability to look, same philosophy as AdminRequestsController's
    /// confidential-comment rule) — enough to LIST and VIEW, not to complete one. Deliberately
    /// narrower than "any Administration access" (a plain Lecteur no longer sees this section): the
    /// standalone D365Approvals app is meant to be reachable only by matched approvers and IT
    /// Personnel viewers, not by every Administration reader.</summary>
    private async Task<bool> CanViewAsync(CancellationToken ct) =>
        await _approvers.HasAnyAccessAsync(User.GetObjectId(), ct) ||
        await _viewers.HasAnyAccessAsync(User.GetObjectId(), ct) ||
        await _appUsers.IsAdminAsync(User.GetObjectId(), ct);

    /// <summary>Which D365ApprovalRoles role is authorized to act RIGHT NOW, given the request's
    /// path and the approval's current status — null once it's in a terminal state (nobody can act
    /// further).</summary>
    private static string? CurrentStageRole(bool isDynawayPath, D365ApprovalStatus status) => status switch
    {
        D365ApprovalStatus.Pending => isDynawayPath ? D365ApprovalRoles.Dynaway : D365ApprovalRoles.Stage1,
        D365ApprovalStatus.Stage1Approved => D365ApprovalRoles.Stage2,
        _ => null
    };

    /// <summary>True if the caller holds whichever role is authorized at the approval's current
    /// stage — the actual gate behind Complete/ConfirmStage2/Reject.</summary>
    private async Task<bool> CanActAtCurrentStageAsync(bool isDynawayPath, D365ApprovalStatus status, CancellationToken ct)
    {
        var role = CurrentStageRole(isDynawayPath, status);
        return role is not null && await _approvers.HasRoleAsync(User.GetObjectId(), role, ct);
    }

    /// <summary>Same current-stage rule as Complete/ConfirmStage2/Reject, plus an AppUsers Admin
    /// even when they aren't the matched approver — cancelling is the only way out for a request
    /// nobody is matched to act on (e.g. a misconfigured D365Approvers table), so it needs a safety
    /// net the others deliberately don't have.</summary>
    private async Task<bool> CanCancelAsync(bool isDynawayPath, D365ApprovalStatus status, CancellationToken ct) =>
        await CanActAtCurrentStageAsync(isDynawayPath, status, ct) ||
        await _appUsers.IsAdminAsync(User.GetObjectId(), ct);

    [HttpGet]
    public async Task<ActionResult<List<D365AccessApprovalSummaryDto>>> List(CancellationToken ct)
    {
        if (!await CanViewAsync(ct)) return Forbid();

        var approvals = await _db.D365AccessApprovals.AsNoTracking()
            .Include(a => a.Request).ThenInclude(r => r.OnboardingDetail)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var employeeNames = await _db.RequestEmployees.AsNoTracking()
            .Where(e => approvals.Select(a => a.RequestEmployeeId).Contains(e.RequestEmployeeId))
            .ToDictionaryAsync(e => e.RequestEmployeeId, e => e.NameSnapshot, ct);

        var employeeWorkdayIds = await _db.RequestEmployees.AsNoTracking()
            .Where(e => approvals.Select(a => a.RequestEmployeeId).Contains(e.RequestEmployeeId))
            .ToDictionaryAsync(e => e.RequestEmployeeId, e => e.WorkdayEmployeeId, ct);

        var workdayIds = employeeWorkdayIds.Values.Distinct().ToList();
        var workdayInfoByEmployee = await _workday.WorkdayDemographics
            .Where(w => w.PrimaryJob == true && workdayIds.Contains(w.EmployeeId))
            .Select(w => new { w.EmployeeId, w.PositionTitle, w.ManagerId, w.Manager })
            .ToDictionaryAsync(w => w.EmployeeId, ct);

        // Batched, same as Detail()'s single-employee lookup — resolve the manager's PREFERRED name
        // where possible, falling back to Workday's own raw Manager string when the manager isn't
        // (or is no longer) a primary-job row itself.
        var managerIds = workdayInfoByEmployee.Values
            .Select(w => w.ManagerId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var managerNames = await _workday.WorkdayDemographics
            .Where(w => w.PrimaryJob == true && managerIds.Contains(w.EmployeeId))
            .Select(w => new { w.EmployeeId, w.FirstName, w.PreferredFirstName, w.LastName })
            .ToDictionaryAsync(w => w.EmployeeId, w => $"{w.PreferredFirstName ?? w.FirstName} {w.LastName}", ct);

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
            var workdayInfo = workdayId is not null ? workdayInfoByEmployee.GetValueOrDefault(workdayId) : null;

            string? managerName = workdayInfo?.Manager;
            if (!string.IsNullOrWhiteSpace(workdayInfo?.ManagerId) && managerNames.TryGetValue(workdayInfo.ManagerId, out var resolvedManagerName))
            {
                managerName = resolvedManagerName;
            }

            return new D365AccessApprovalSummaryDto
            {
                RequestId = a.RequestId,
                RequestNumber = a.Request.RequestNumber,
                EmployeeName = employeeNames.GetValueOrDefault(a.RequestEmployeeId, "?"),
                PositionTitle = workdayInfo?.PositionTitle,
                ManagerName = managerName,
                RequesterName = a.Request.CreatedByDisplayName,
                StartDate = a.Request.OnboardingDetail?.DateEntreePrevue,
                Status = a.Status.ToString(),
                IsDynawayPath = a.NeedsDynaway,
                CreatedAt = a.CreatedAt,
                Stage1ApprovedByDisplayName = a.Stage1ApprovedByDisplayName,
                Stage1ApprovedAt = a.Stage1ApprovedAt,
                CompletedAt = a.CompletedAt,
                CompletedByDisplayName = a.CompletedByDisplayName,
                CancelledAt = a.CancelledAt,
                CancelledByDisplayName = a.CancelledByDisplayName,
                CancelReason = a.CancelReason,
                RejectedAt = a.RejectedAt,
                RejectedByDisplayName = a.RejectedByDisplayName,
                RejectReason = a.RejectReason,
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
            .Select(w => new { w.JobCode, w.JobProfile, w.PositionTitle, w.CostCenter, w.WorkEmail, w.Email, w.ManagerId, w.Manager })
            .FirstOrDefaultAsync(ct);

        if (!await CanViewAsync(ct)) return Forbid();
        var isDynawayPath = approval.NeedsDynaway;
        var canActNow = await CanActAtCurrentStageAsync(isDynawayPath, approval.Status, ct);
        var canCancel = await CanCancelAsync(isDynawayPath, approval.Status, ct);
        var isActionableStatus = approval.Status is D365ApprovalStatus.Pending or D365ApprovalStatus.Stage1Approved;

        var managerName = await ResolveManagerNameAsync(workdayInfo?.ManagerId, workdayInfo?.Manager, ct);
        var peers = await BuildPeersAsync(employee.WorkdayEmployeeId, workdayInfo?.JobCode, workdayInfo?.PositionTitle, ct);

        return Ok(new D365AccessApprovalDetailDto
        {
            RequestId = approval.RequestId,
            RequestNumber = approval.Request.RequestNumber,
            Status = approval.Status.ToString(),
            IsDynawayPath = isDynawayPath,
            CancelledByDisplayName = approval.CancelledByDisplayName,
            CancelledAt = approval.CancelledAt,
            CancelReason = approval.CancelReason,
            Stage1ApprovedByDisplayName = approval.Stage1ApprovedByDisplayName,
            Stage1ApprovedAt = approval.Stage1ApprovedAt,
            RejectedByDisplayName = approval.RejectedByDisplayName,
            RejectedAt = approval.RejectedAt,
            RejectReason = approval.RejectReason,
            CanComplete = canActNow && approval.Status == D365ApprovalStatus.Pending,
            CanConfirmStage2 = canActNow && approval.Status == D365ApprovalStatus.Stage1Approved,
            CanReject = canActNow && isActionableStatus,
            CanCancel = canCancel && isActionableStatus,
            RequesterName = approval.Request.CreatedByDisplayName,
            EmployeeName = employee.NameSnapshot,
            EmployeeEmail = workdayInfo?.WorkEmail ?? workdayInfo?.Email,
            ManagerName = managerName,
            PositionTitle = workdayInfo?.PositionTitle ?? employee.PositionSnapshot,
            JobCode = workdayInfo?.JobCode ?? employee.CodeEmploiSnapshot,
            Departement = employee.DepartementSnapshot,
            StartDate = approval.Request.OnboardingDetail?.DateEntreePrevue,
            JobTitleEnglish = approval.JobTitleEnglish ?? BuildDefaultJobTitle(workdayInfo?.JobProfile, workdayInfo?.PositionTitle ?? employee.PositionSnapshot),
            LegalEntity = FixedLegalEntity,
            DepartmentNumber = approval.DepartmentNumber ?? workdayInfo?.CostCenter,
            AccessType = approval.AccessType ?? "New Access",
            ApprovalLimit = approval.ApprovalLimit,
            ApAccessDetails = approval.ApAccessDetails,
            AdditionalLegalEntities = approval.AdditionalLegalEntities,
            DefaultShippingAddress = approval.DefaultShippingAddress,
            Comments = approval.Comments,
            LevyEmployee = approval.LevyEmployee,
            Roles = approval.Roles.Select(r => r.Role).OrderBy(r => r).ToList(),
            RoleCatalog = TdxD365RoleCheckboxes.All.ToList(),
            Peers = peers
        });
    }

    /// <summary>Resolves the manager's PREFERRED name where possible (a Workday primary-job row of
    /// their own), falling back to Workday's own raw Manager string when the manager isn't (or is
    /// no longer) one — shared by Detail() and the ad-hoc prefill endpoint; List() has its own
    /// batched version of the same lookup since it's resolving many employees at once.</summary>
    private async Task<string?> ResolveManagerNameAsync(string? managerId, string? fallbackManagerName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(managerId)) return fallbackManagerName;

        var manager = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == managerId && w.PrimaryJob == true)
            .Select(w => new { w.FirstName, w.PreferredFirstName, w.LastName })
            .FirstOrDefaultAsync(ct);

        return manager is not null ? $"{manager.PreferredFirstName ?? manager.FirstName} {manager.LastName}" : fallbackManagerName;
    }

    /// <summary>"{Job_Profile} - {Position_Title}" — Workday's own Job_Profile is already English
    /// (e.g. "0115U - Maintenance Attendant"), unlike Position_Title (French-only at Tremblant), so
    /// this gives the approver a starting point with both the English label and the French title
    /// for context, which they can trim down. Falls back gracefully if either half is missing.</summary>
    private static string? BuildDefaultJobTitle(string? jobProfile, string? positionTitle)
    {
        var parts = new[] { jobProfile, positionTitle }.Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(" - ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    /// <summary>Same Job Code AND Position Title, excluding this employee, and the D365 security
    /// roles each already holds (D365UserSecurityRole, joined by Workday EmployeeId — see
    /// DiscrepanciesController for the same join pattern). Peers with roles sort first: they're the
    /// useful reference signal.
    ///
    /// Filtered to EmploymentStatus != "Terminated" — same rule as EmployeesController's search
    /// (not == "Active", since "Inactive" covers on-leave/layoff, which is still a real employee
    /// whose D365 roles are legitimate reference data). Without this, a peer who left Tremblant
    /// years ago still shows up as if their old roles were a current, meaningful comparison.</summary>
    private async Task<List<D365PeerRoleDto>> BuildPeersAsync(string employeeWorkdayId, string? jobCode, string? positionTitle, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(jobCode) || string.IsNullOrWhiteSpace(positionTitle)) return [];

        var peers = await _workday.WorkdayDemographics
            .Where(w => w.PrimaryJob == true && w.EmploymentStatus != "Terminated"
                && w.JobCode == jobCode && w.PositionTitle == positionTitle && w.EmployeeId != employeeWorkdayId)
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

    /// <summary>The Envoyer action — Dynaway approver on a Dynaway request (single-stage, same
    /// behavior as before this redesign), or Stage1 approver on every other request. Saves what the
    /// approver entered; on the Dynaway path this immediately attempts the real TDX ticket (approval
    /// marked Completed either way — the human decision is made; whether the downstream TDX call
    /// itself succeeded is reported back and, if not, handled by the normal Administration/Réessayer
    /// path). On the non-Dynaway path this instead advances to Stage1Approved and emails the Stage2
    /// approvers — no TDX call happens until ConfirmStage2.</summary>
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
            return Conflict(new CompleteD365AccessApprovalResultDto { Succeeded = false, Error = "Cette approbation n'est plus en attente de cette étape." });
        }

        var isDynawayPath = approval.NeedsDynaway;
        if (!await CanActAtCurrentStageAsync(isDynawayPath, approval.Status, ct)) return Forbid();

        var employee = approval.Request.Employees.FirstOrDefault(e => e.RequestEmployeeId == approval.RequestEmployeeId);
        var workdayInfo = employee is null ? null : await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.PrimaryJob == true)
            .Select(w => new { w.CostCenter })
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(dto.JobTitleEnglish))
        {
            return BadRequest("Le titre du poste (anglais) est requis.");
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
        approval.LegalEntity = FixedLegalEntity;
        approval.DepartmentNumber = workdayInfo?.CostCenter;
        approval.ApprovalLimit = dto.ApprovalLimit;
        approval.LevyEmployee = dto.LevyEmployee;
        approval.ApAccessDetails = string.IsNullOrWhiteSpace(dto.ApAccessDetails) ? null : dto.ApAccessDetails.Trim();
        approval.AdditionalLegalEntities = string.IsNullOrWhiteSpace(dto.AdditionalLegalEntities) ? null : dto.AdditionalLegalEntities.Trim();
        approval.DefaultShippingAddress = string.IsNullOrWhiteSpace(dto.DefaultShippingAddress) ? null : dto.DefaultShippingAddress.Trim();
        approval.Comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
        approval.Roles.Clear();
        foreach (var role in dto.Roles.Distinct())
        {
            approval.Roles.Add(new D365AccessApprovalRole { Role = role });
        }

        var actorDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();

        if (!isDynawayPath)
        {
            approval.Status = D365ApprovalStatus.Stage1Approved;
            approval.Stage1ApprovedByObjectId = User.GetObjectId();
            approval.Stage1ApprovedByDisplayName = actorDisplayName;
            approval.Stage1ApprovedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("D365 Stage1 approver {Approver} approved request {RequestNumber} — awaiting Stage2", User.GetObjectId(), approval.Request.RequestNumber);

            await _orchestration.NotifyD365Stage2ApproversAsync(approval.Request, approval, ct);

            return Ok(new CompleteD365AccessApprovalResultDto { Succeeded = true });
        }

        approval.Status = D365ApprovalStatus.Completed;
        approval.CompletedByObjectId = User.GetObjectId();
        approval.CompletedByDisplayName = actorDisplayName;
        approval.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("D365 Dynaway approver {Approver} completed approval for request {RequestNumber}", User.GetObjectId(), approval.Request.RequestNumber);

        var result = await _orchestration.CreateD365AccessTicketAsync(approval.Request, approval, ct);

        return Ok(new CompleteD365AccessApprovalResultDto
        {
            Succeeded = result.Succeeded,
            TicketNumber = result.TicketNumber,
            Error = result.Error
        });
    }

    /// <summary>The Stage2 approver's final sign-off, non-Dynaway requests only — a pure
    /// confirmation of whatever Stage1 already filled in (no fields to re-enter). Marks the
    /// approval Completed and attempts the real TDX ticket, same as Complete does on the Dynaway
    /// path.</summary>
    [HttpPost("{requestId:int}/confirm-stage2")]
    public async Task<ActionResult<CompleteD365AccessApprovalResultDto>> ConfirmStage2(int requestId, CancellationToken ct)
    {
        var approval = await _db.D365AccessApprovals
            .Include(a => a.Request).ThenInclude(r => r.Employees)
            .FirstOrDefaultAsync(a => a.RequestId == requestId, ct);
        if (approval is null) return NotFound();

        if (approval.Status != D365ApprovalStatus.Stage1Approved)
        {
            return Conflict(new CompleteD365AccessApprovalResultDto { Succeeded = false, Error = "Cette approbation n'est pas en attente d'une confirmation Stage2." });
        }

        var isDynawayPath = approval.NeedsDynaway;
        if (!await CanActAtCurrentStageAsync(isDynawayPath, approval.Status, ct)) return Forbid();

        approval.Status = D365ApprovalStatus.Completed;
        approval.CompletedByObjectId = User.GetObjectId();
        approval.CompletedByDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        approval.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("D365 Stage2 approver {Approver} confirmed request {RequestNumber}", User.GetObjectId(), approval.Request.RequestNumber);

        var result = await _orchestration.CreateD365AccessTicketAsync(approval.Request, approval, ct);

        return Ok(new CompleteD365AccessApprovalResultDto
        {
            Succeeded = result.Succeeded,
            TicketNumber = result.TicketNumber,
            Error = result.Error
        });
    }

    /// <summary>A Stage1 or Stage2 approver actively declining the request — terminal, no TDX
    /// ticket is ever created. Distinct from Cancel: this is an approver's own "no" (reason
    /// required, requester notified why), not an administrative withdrawal.</summary>
    [HttpPost("{requestId:int}/reject")]
    public async Task<IActionResult> Reject(int requestId, RejectD365AccessApprovalDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest("Un motif de rejet est requis.");

        var approval = await _db.D365AccessApprovals
            .Include(a => a.Request).ThenInclude(r => r.Employees)
            .FirstOrDefaultAsync(a => a.RequestId == requestId, ct);
        if (approval is null) return NotFound();

        if (approval.Status is not (D365ApprovalStatus.Pending or D365ApprovalStatus.Stage1Approved))
        {
            return Conflict("Cette demande n'est plus en attente.");
        }

        var isDynawayPath = approval.NeedsDynaway;
        if (!await CanActAtCurrentStageAsync(isDynawayPath, approval.Status, ct)) return Forbid();

        approval.Status = D365ApprovalStatus.Rejected;
        approval.RejectedByObjectId = User.GetObjectId();
        approval.RejectedByDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        approval.RejectedAt = DateTime.UtcNow;
        approval.RejectReason = dto.Reason.Trim();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("D365 approver {Approver} rejected request {RequestNumber}", User.GetObjectId(), approval.Request.RequestNumber);

        await _orchestration.NotifyRequesterOfD365RejectionAsync(approval.Request, approval, ct);

        return NoContent();
    }

    /// <summary>Marks a Pending/Stage1Approved approval Cancelled — no TDX ticket is ever created
    /// for it. Gated by CanCancelAsync (whoever is authorized at the current stage OR an AppUsers
    /// Admin), broader than Complete/ConfirmStage2/Reject's matched-approver-only rule, since a
    /// request nobody is matched to act on would otherwise be stuck forever either way.</summary>
    [HttpPost("{requestId:int}/cancel")]
    public async Task<IActionResult> Cancel(int requestId, CancelD365AccessApprovalDto dto, CancellationToken ct)
    {
        var approval = await _db.D365AccessApprovals
            .Include(a => a.Request).ThenInclude(r => r.Employees)
            .FirstOrDefaultAsync(a => a.RequestId == requestId, ct);
        if (approval is null) return NotFound();

        if (approval.Status is not (D365ApprovalStatus.Pending or D365ApprovalStatus.Stage1Approved))
        {
            return Conflict("Cette demande n'est plus en attente.");
        }

        var isDynawayPath = approval.NeedsDynaway;
        if (!await CanCancelAsync(isDynawayPath, approval.Status, ct)) return Forbid();

        approval.Status = D365ApprovalStatus.Cancelled;
        approval.CancelledByObjectId = User.GetObjectId();
        approval.CancelledByDisplayName = _ad.GetUserInfo(User.GetSamAccountName()).DisplayName ?? User.GetObjectId();
        approval.CancelledAt = DateTime.UtcNow;
        approval.CancelReason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("D365 approver {Approver} cancelled approval for request {RequestNumber}", User.GetObjectId(), approval.Request.RequestNumber);

        return NoContent();
    }

    /// <summary>Powers the standalone D365AccessRequest app's employee picker: once someone picks a
    /// Workday employee, this returns everything to prefill and display — same shape as Detail()'s
    /// read-only section, but there is no existing approval/request to read it FROM yet.
    /// Open to any authenticated employee (see AllowedAccessTypes/SubmitAdHoc below) — submitting a
    /// request no longer requires being a D365Approver yourself; the D365Approvals "Envoyer" step
    /// remains the actual gate before anything reaches TDX.</summary>
    [HttpGet("adhoc/prefill")]
    public async Task<ActionResult<D365AdHocPrefillDto>> AdHocPrefill([FromQuery] string workdayEmployeeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workdayEmployeeId)) return BadRequest("workdayEmployeeId est requis.");

        var workdayInfo = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == workdayEmployeeId && w.PrimaryJob == true)
            .Select(w => new
            {
                w.JobCode, w.JobProfile, w.PositionTitle, w.JobFamilyGroup, w.CostCenter,
                w.WorkEmail, w.Email, w.ManagerId, w.Manager, w.FirstName, w.PreferredFirstName, w.LastName
            })
            .FirstOrDefaultAsync(ct);
        if (workdayInfo is null) return NotFound("Employé introuvable (ou son emploi n'est pas l'emploi principal).");

        var managerName = await ResolveManagerNameAsync(workdayInfo.ManagerId, workdayInfo.Manager, ct);
        var peers = await BuildPeersAsync(workdayEmployeeId, workdayInfo.JobCode, workdayInfo.PositionTitle, ct);

        return Ok(new D365AdHocPrefillDto
        {
            WorkdayEmployeeId = workdayEmployeeId,
            EmployeeName = $"{workdayInfo.PreferredFirstName ?? workdayInfo.FirstName} {workdayInfo.LastName}",
            EmployeeEmail = workdayInfo.WorkEmail ?? workdayInfo.Email,
            ManagerName = managerName,
            PositionTitle = workdayInfo.PositionTitle,
            JobCode = workdayInfo.JobCode,
            Departement = workdayInfo.JobFamilyGroup,
            LegalEntity = FixedLegalEntity,
            DepartmentNumber = workdayInfo.CostCenter,
            JobTitleEnglishSuggestion = BuildDefaultJobTitle(workdayInfo.JobProfile, workdayInfo.PositionTitle),
            RoleCatalog = TdxD365RoleCheckboxes.All.ToList(),
            Peers = peers,
            AccessTypeCatalog = AllowedAccessTypes.ToList()
        });
    }

    /// <summary>Every distinct Workday Cost_Center currently in use — powers the ad-hoc form's
    /// "Numéro de département" dropdown, so a requester can pick a department other than the
    /// employee's own (AdHocPrefill's DepartmentNumber is only the default). Same "distinct,
    /// non-blank, sorted" shape as any other Workday-derived catalog in this app.</summary>
    [HttpGet("adhoc/cost-centers")]
    public async Task<ActionResult<List<string>>> AdHocCostCenters(CancellationToken ct)
    {
        var costCenters = await _workday.WorkdayDemographics
            .Where(w => w.CostCenter != null && w.CostCenter != "")
            .Select(w => w.CostCenter!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return Ok(costCenters);
    }

    /// <summary>AD people search for the ad-hoc form's "Nom du gestionnaire" picker — same
    /// SearchAccounts call as every other "add a user" picker in this app family
    /// (AppUsersController/D365ApproversController/D365ViewersController's own ad-search actions),
    /// but open to any authenticated employee rather than admin-gated, matching this controller's
    /// other adhoc/* actions: submitting a request has never required being a D365Approver, and
    /// picking who it should route to shouldn't either.</summary>
    [HttpGet("adhoc/ad-search")]
    public ActionResult<List<AdAccountDto>> AdHocAdSearch([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Ok(new List<AdAccountDto>());

        var hits = _ad.SearchAccounts(q.Trim(), 15);
        return Ok(hits.Select(a => new AdAccountDto
        {
            Sam = a.Sam,
            DisplayName = a.Cn ?? a.Sam,
            Email = a.Email
        }).ToList());
    }

    /// <summary>Submits a brand-new, fully-filled-out D365 access request for an employee who never
    /// went through the onboarding/réactivation wizard — see SubmitAdHocD365AccessDto's doc comment.
    /// Creates a minimal Request (RequestType.D365AccessOnly — no OnboardingDetail/AccessDetail/etc,
    /// it exists only to give the approval a Request to hang off of, matching every other approval's
    /// shape) + one RequestEmployee snapshot + a Pending D365AccessApproval carrying every field the
    /// requester entered, then emails matched approvers exactly like the wizard-driven path does.
    /// Open to any authenticated employee — the requester's own identity is recorded from their AD
    /// claims regardless of whether they're a D365Approver; the maker-checker split still holds
    /// because nothing reaches TDX until a real D365Approver reviews it and presses "Envoyer" in
    /// D365Approvals.</summary>
    [HttpPost("adhoc")]
    public async Task<ActionResult<SubmitAdHocD365AccessResultDto>> SubmitAdHoc(SubmitAdHocD365AccessDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.WorkdayEmployeeId)) return BadRequest("L'employé est requis.");
        if (!AllowedAccessTypes.Contains(dto.AccessType)) return BadRequest("Le type d'accès est requis.");
        if (string.IsNullOrWhiteSpace(dto.JobTitleEnglish)) return BadRequest("Le titre du poste (anglais) est requis.");
        var invalidRoles = dto.Roles.Except(TdxD365RoleCheckboxes.All).ToList();
        if (invalidRoles.Count > 0) return BadRequest($"Rôle(s) inconnu(s) : {string.Join(", ", invalidRoles)}");

        var requesterInfo = _ad.GetUserInfo(User.GetSamAccountName());
        var allowedLimits = !string.IsNullOrWhiteSpace(requesterInfo.Email) && ElevatedApprovalLimitEmails.Contains(requesterInfo.Email, StringComparer.OrdinalIgnoreCase)
            ? ElevatedApprovalLimits
            : StandardApprovalLimits;
        if (!allowedLimits.Contains(dto.ApprovalLimit))
        {
            return BadRequest($"Limite d'approbation invalide : {dto.ApprovalLimit} $. Valeurs permises : {string.Join(", ", allowedLimits)}.");
        }

        var workdayInfo = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == dto.WorkdayEmployeeId && w.PrimaryJob == true)
            .Select(w => new
            {
                w.JobCode, w.PositionTitle, w.JobFamilyGroup, w.CostCenter, w.TimeType, w.WorkerType,
                w.Manager, w.FirstName, w.PreferredFirstName, w.LastName
            })
            .FirstOrDefaultAsync(ct);
        if (workdayInfo is null) return BadRequest("Employé introuvable (ou son emploi n'est pas l'emploi principal).");

        var employeeName = $"{workdayInfo.PreferredFirstName ?? workdayInfo.FirstName} {workdayInfo.LastName}";

        var request = new Request
        {
            RequestNumber = await _requestNumbers.GenerateAsync(RequestType.D365AccessOnly, ct),
            RequestType = RequestType.D365AccessOnly,
            Status = RequestStatus.Soumise,
            CreatedByObjectId = User.GetObjectId(),
            CreatedByDisplayName = requesterInfo.DisplayName ?? User.GetObjectId(),
            RequesterEmail = requesterInfo.Email,
            CreatedAt = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        // Both default to the employee's own Workday values (same as before this DTO carried them
        // at all) but the requester may have picked a different cost center / AD account on the
        // ad-hoc form — see SubmitAdHocD365AccessDto's doc comments.
        var managerName = string.IsNullOrWhiteSpace(dto.ManagerName) ? workdayInfo.Manager : dto.ManagerName.Trim();
        var departmentNumber = string.IsNullOrWhiteSpace(dto.DepartmentNumber) ? workdayInfo.CostCenter : dto.DepartmentNumber.Trim();

        request.Employees.Add(new RequestEmployee
        {
            WorkdayEmployeeId = dto.WorkdayEmployeeId,
            NameSnapshot = employeeName,
            PositionSnapshot = workdayInfo.PositionTitle,
            DepartementSnapshot = workdayInfo.JobFamilyGroup,
            CodeEmploiSnapshot = workdayInfo.JobCode,
            TypeEmploiSnapshot = workdayInfo.TimeType != null && workdayInfo.WorkerType != null ? $"{workdayInfo.TimeType} — {workdayInfo.WorkerType}" : workdayInfo.TimeType,
            GestionnaireSnapshot = managerName,
            IsPrimary = true
        });
        _db.Requests.Add(request);
        await _db.SaveChangesAsync(ct);

        var comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
        if (dto.NeedsDynaway)
        {
            comments = comments is null ? TicketOrchestrationService.DynawayCommentDefault : $"{TicketOrchestrationService.DynawayCommentDefault}\n{comments}";
        }

        var approval = new D365AccessApproval
        {
            RequestId = request.RequestId,
            RequestEmployeeId = request.Employees.Single().RequestEmployeeId,
            Status = D365ApprovalStatus.Pending,
            NeedsDynaway = dto.NeedsDynaway,
            AccessType = dto.AccessType,
            JobTitleEnglish = dto.JobTitleEnglish.Trim(),
            LegalEntity = FixedLegalEntity,
            DepartmentNumber = departmentNumber,
            ApprovalLimit = dto.ApprovalLimit,
            LevyEmployee = dto.LevyEmployee,
            ApAccessDetails = string.IsNullOrWhiteSpace(dto.ApAccessDetails) ? null : dto.ApAccessDetails.Trim(),
            AdditionalLegalEntities = string.IsNullOrWhiteSpace(dto.AdditionalLegalEntities) ? null : dto.AdditionalLegalEntities.Trim(),
            DefaultShippingAddress = string.IsNullOrWhiteSpace(dto.DefaultShippingAddress) ? null : dto.DefaultShippingAddress.Trim(),
            Comments = comments,
            CreatedAt = DateTime.UtcNow
        };
        foreach (var role in dto.Roles.Distinct())
        {
            approval.Roles.Add(new D365AccessApprovalRole { Role = role });
        }
        _db.D365AccessApprovals.Add(approval);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("D365 approver {Approver} submitted ad-hoc D365 access request {RequestNumber} for {Employee}", User.GetObjectId(), request.RequestNumber, employeeName);

        var routingRole = dto.NeedsDynaway ? D365ApprovalRoles.Dynaway : D365ApprovalRoles.Stage1;
        await _orchestration.NotifyD365ApproversOfAdHocRequestAsync(request, approval, routingRole, ct);

        return Ok(new SubmitAdHocD365AccessResultDto { RequestId = request.RequestId, RequestNumber = request.RequestNumber });
    }
}
