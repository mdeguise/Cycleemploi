namespace TremblantLifecycle.Api.Services;

/// <summary>Username/Password deliberately left empty here — the real values live only in
/// appsettings.Production.json on the server (gitignored, never committed). AppId/FormId/AccountId/
/// ResponsibleGroupId/ResponsibleGroupName confirmed against the real TDX instance: FormID 1162 is
/// "Quick Incident" under AppID 278 ("OneIT"), AccountID 8811 is "Information Technology".
/// D365AccessFormId/D365AccessResponsibleGroupId/Name are for the separate "D365 - Access" form
/// (FormID 10799) — confirmed against real historical tickets on that form: newly-created tickets
/// land in ResponsibleGroupID 4078 ("ENT - FinApp Triage") before being triaged elsewhere.
/// HelpForm* fields are for the "Besoin d'aide?" link — the public TDX client-portal Incident form
/// (a different host/domain than BaseUrl, which is the TDWebApi REST endpoint), confirmed canonical
/// by the user against a real example link they generated from their own TDX session.</summary>
public class TdxOptions
{
    public string BaseUrl { get; set; } = "https://get.alterra.support/TDWebApi";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int AppId { get; set; }
    public int FormId { get; set; }
    public int AccountId { get; set; }
    public int ResponsibleGroupId { get; set; }
    public string ResponsibleGroupName { get; set; } = "";
    public int D365AccessFormId { get; set; }
    public int D365AccessResponsibleGroupId { get; set; }
    public string D365AccessResponsibleGroupName { get; set; } = "";
    public string HelpFormBaseUrl { get; set; } = "https://us1.teamdynamix.com/tdapp/form/Incident";
    public int HelpFormResortId { get; set; }
    public int HelpFormCategoryId { get; set; }
    public int HelpFormServiceId { get; set; }
    public int HelpFormOfferingId { get; set; }
    public string HelpFormCustomer { get; set; } = "";
    public string HelpFormItemId { get; set; } = "";
}
