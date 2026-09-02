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
    /// an approver input.</summary>
    private const string FixedLegalEntity = "6201";

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
            .Select(w => new { w.JobCode, w.JobProfile, w.PositionTitle, w.CostCenter, w.WorkEmail, w.Email, w.ManagerId, w.Manager })
            .FirstOrDefaultAsync(ct);

        if (!await CanViewAsync(ct)) return Forbid();
        var canComplete = await _approvers.CanActOnAsync(User.GetObjectId(), workdayInfo?.PositionTitle, ct);

        var managerName = await ResolveManagerNameAsync(workdayInfo?.ManagerId, workdayInfo?.Manager, ct);
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
            JobTitleEnglish = approval.JobTitleEnglish ?? BuildDefaultJobTitle(workdayInfo?.JobProfile, workdayInfo?.PositionTitle ?? employee.PositionSnapshot),
            LegalEntity = FixedLegalEntity,
            DepartmentNumber = approval.DepartmentNumber ?? workdayInfo?.CostCenter,
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
        var workdayInfo = employee is null ? null : await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == employee.WorkdayEmployeeId && w.PrimaryJob == true)
            .Select(w => new { w.PositionTitle, w.CostCenter })
            .FirstOrDefaultAsync(ct);

        if (!await _approvers.CanActOnAsync(User.GetObjectId(), workdayInfo?.PositionTitle, ct)) return Forbid();

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

    /// <summary>Powers the standalone D365AccessRequest app's employee picker: once a D365 Approver
    /// picks someone from Workday, this returns everything to prefill and display — same shape as
    /// Detail()'s read-only section, but there is no existing approval/request to read it FROM yet.
    /// Gated on being ANY D365Approver (not the narrower CanViewAsync, which also admits D365Viewer
    /// and AppUsers Admin — creating a new request is an approver action, not a viewing one).</summary>
    [HttpGet("adhoc/prefill")]
    public async Task<ActionResult<D365AdHocPrefillDto>> AdHocPrefill([FromQuery] string workdayEmployeeId, CancellationToken ct)
    {
        if (!await _approvers.HasAnyAccessAsync(User.GetObjectId(), ct)) return Forbid();
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
            Peers = peers
        });
    }

    /// <summary>Submits a brand-new, fully-filled-out D365 access request for an employee who never
    /// went through the onboarding/réactivation wizard — see SubmitAdHocD365AccessDto's doc comment.
    /// Creates a minimal Request (RequestType.D365AccessOnly — no OnboardingDetail/AccessDetail/etc,
    /// it exists only to give the approval a Request to hang off of, matching every other approval's
    /// shape) + one RequestEmployee snapshot + a Pending D365AccessApproval carrying every field the
    /// requester entered, then emails matched approvers exactly like the wizard-driven path does.</summary>
    [HttpPost("adhoc")]
    public async Task<ActionResult<SubmitAdHocD365AccessResultDto>> SubmitAdHoc(SubmitAdHocD365AccessDto dto, CancellationToken ct)
    {
        if (!await _approvers.HasAnyAccessAsync(User.GetObjectId(), ct)) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.WorkdayEmployeeId)) return BadRequest("L'employé est requis.");
        if (string.IsNullOrWhiteSpace(dto.JobTitleEnglish)) return BadRequest("Le titre du poste (anglais) est requis.");
        if (dto.ApprovalLimit < 0) return BadRequest("La limite d'approbation ne peut pas être négative.");
        var invalidRoles = dto.Roles.Except(TdxD365RoleCheckboxes.All).ToList();
        if (invalidRoles.Count > 0) return BadRequest($"Rôle(s) inconnu(s) : {string.Join(", ", invalidRoles)}");

        var workdayInfo = await _workday.WorkdayDemographics
            .Where(w => w.EmployeeId == dto.WorkdayEmployeeId && w.PrimaryJob == true)
            .Select(w => new
            {
                w.JobCode, w.PositionTitle, w.JobFamilyGroup, w.CostCenter, w.TimeType, w.WorkerType,
                w.Manager, w.FirstName, w.PreferredFirstName, w.LastName
            })
            .FirstOrDefaultAsync(ct);
        if (workdayInfo is null) return BadRequest("Employé introuvable (ou son emploi n'est pas l'emploi principal).");

        var requesterInfo = _ad.GetUserInfo(User.GetSamAccountName());
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
        request.Employees.Add(new RequestEmployee
        {
            WorkdayEmployeeId = dto.WorkdayEmployeeId,
            NameSnapshot = employeeName,
            PositionSnapshot = workdayInfo.PositionTitle,
            DepartementSnapshot = workdayInfo.JobFamilyGroup,
            CodeEmploiSnapshot = workdayInfo.JobCode,
            TypeEmploiSnapshot = workdayInfo.TimeType != null && workdayInfo.WorkerType != null ? $"{workdayInfo.TimeType} — {workdayInfo.WorkerType}" : workdayInfo.TimeType,
            GestionnaireSnapshot = workdayInfo.Manager,
            IsPrimary = true
        });
        _db.Requests.Add(request);
        await _db.SaveChangesAsync(ct);

        var approval = new D365AccessApproval
        {
            RequestId = request.RequestId,
            RequestEmployeeId = request.Employees.Single().RequestEmployeeId,
            Status = D365ApprovalStatus.Pending,
            JobTitleEnglish = dto.JobTitleEnglish.Trim(),
            LegalEntity = FixedLegalEntity,
            DepartmentNumber = workdayInfo.CostCenter,
            ApprovalLimit = dto.ApprovalLimit,
            LevyEmployee = dto.LevyEmployee,
            ApAccessDetails = string.IsNullOrWhiteSpace(dto.ApAccessDetails) ? null : dto.ApAccessDetails.Trim(),
            AdditionalLegalEntities = string.IsNullOrWhiteSpace(dto.AdditionalLegalEntities) ? null : dto.AdditionalLegalEntities.Trim(),
            DefaultShippingAddress = string.IsNullOrWhiteSpace(dto.DefaultShippingAddress) ? null : dto.DefaultShippingAddress.Trim(),
            Comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        foreach (var role in dto.Roles.Distinct())
        {
            approval.Roles.Add(new D365AccessApprovalRole { Role = role });
        }
        _db.D365AccessApprovals.Add(approval);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("D365 approver {Approver} submitted ad-hoc D365 access request {RequestNumber} for {Employee}", User.GetObjectId(), request.RequestNumber, employeeName);

        await _orchestration.NotifyD365ApproversOfAdHocRequestAsync(request, approval, workdayInfo.PositionTitle, ct);

        return Ok(new SubmitAdHocD365AccessResultDto { RequestId = request.RequestId, RequestNumber = request.RequestNumber });
    }
}
