namespace TremblantLifecycle.Api.Services;

/// <summary>Config for the app's own identity — currently just its deployed base URL, needed to
/// build a working link into a notification email (there is nowhere else in the backend that knows
/// what origin it's being served from).</summary>
public class AppOptions
{
    public string BaseUrl { get; set; } = "";
}
