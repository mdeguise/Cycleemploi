namespace TremblantLifecycle.Api.Models.Dtos;

public class D365JobCodeTemplateSummaryDto
{
    public string JobCode { get; set; } = null!;
    public string? PositionTitle { get; set; }
    public bool IsFilled { get; set; }
}

public class D365JobCodeTemplateDto
{
    public string JobCode { get; set; } = null!;
    public string? PositionTitle { get; set; }
    public string LegalEntity { get; set; } = "";
    public string DepartmentNumber { get; set; } = "";
    public decimal ApprovalLimit { get; set; }
    public string? ApAccessDetails { get; set; }
    public string? AdditionalLegalEntities { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool IsFilled { get; set; }
}

public class UpsertD365JobCodeTemplateDto
{
    public string LegalEntity { get; set; } = null!;
    public string DepartmentNumber { get; set; } = null!;
    public decimal ApprovalLimit { get; set; }
    public string? ApAccessDetails { get; set; }
    public string? AdditionalLegalEntities { get; set; }
    public List<string> Roles { get; set; } = [];
}
