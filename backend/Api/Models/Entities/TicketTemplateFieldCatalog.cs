namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>Fields available on every employee on a request — populated by EmployeeFieldsService
/// (RequestEmployee snapshot + a live Workday lookup). Used inside a Block template's
/// "employeeGroup" blocks, and directly as Inline parts on TDX templates (which always concern
/// exactly one employee).</summary>
public static class TicketTemplateFieldCatalog
{
    public static readonly IReadOnlyList<TicketTemplateField> EmployeeFields =
    [
        new("EmployeeName", "Nom complet de l'employé", TicketFieldCategory.Employee),
        new("EmployeeFirstName", "Prénom", TicketFieldCategory.Employee),
        new("EmployeeLastName", "Nom de famille", TicketFieldCategory.Employee),
        new("EmployeeId", "Numéro d'employé Workday", TicketFieldCategory.Employee),
        new("Poste", "Titre du poste", TicketFieldCategory.Employee),
        new("PositionCode", "Code de poste Workday", TicketFieldCategory.Employee),
        new("Departement", "Département", TicketFieldCategory.Employee),
        new("Gestionnaire", "Gestionnaire", TicketFieldCategory.Employee),
        new("TypeEmploi", "Type d'emploi", TicketFieldCategory.Employee),
        new("CodeEmploi", "Code d'emploi (actuel)", TicketFieldCategory.Employee),
        new("AllJobCodes", "Tous les codes d'emploi déjà occupés", TicketFieldCategory.Employee),
        new("CostCenter", "Centre de coûts", TicketFieldCategory.Employee),
        new("HireDate", "Date d'embauche", TicketFieldCategory.Employee),
        new("SeniorityDate", "Date d'ancienneté", TicketFieldCategory.Employee),
        new("ActiveStatus", "Statut actif (Oui/Non)", TicketFieldCategory.Employee),
        new("LeaveType", "Type de congé", TicketFieldCategory.Employee),
        new("EstimatedLastDayOfLeave", "Dernier jour de congé estimé", TicketFieldCategory.Employee),
        new("EmploymentStatus", "Statut d'emploi", TicketFieldCategory.Employee),
        new("PayGroup", "Groupe de paye", TicketFieldCategory.Employee),
    ];

    public static TicketTemplateField Employee(string key) =>
        EmployeeFields.First(f => f.Key == key);
}
