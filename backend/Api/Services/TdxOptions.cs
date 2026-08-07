namespace TremblantLifecycle.Api.Services;

/// <summary>Username/Password deliberately left empty here — the real values live only in
/// appsettings.Production.json on the server (gitignored, never committed). AppId/FormId/AccountId/
/// ResponsibleGroupId/ResponsibleGroupName confirmed against the real TDX instance: FormID 1162 is
/// "Quick Incident" under AppID 278 ("OneIT"), AccountID 8811 is "Information Technology".</summary>
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
}
