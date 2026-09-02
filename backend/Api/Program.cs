using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Data;
using TremblantLifecycle.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Data ---
// Two separate DbContexts because these are two genuinely different, separately-owned databases
// on the same vm-trm-sql1 instance — see AppDbContext/WorkdayContext doc comments.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AppDb")));
builder.Services.AddDbContext<WorkdayContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WorkdayDb")));

// --- Auth ---
// Windows Integrated Auth (Negotiate/Kerberos) — the app trusts the same AD identity the browser
// and the user's Windows session already carry, instead of an Entra ID app registration. Only
// works when both client and server are on the domain network (matches the VPN-only reachability
// requirement) and the app is hosted on Windows (IIS) — Negotiate from a Linux container needs
// Kerberos keytab/SPN setup that isn't worth the complexity here.
//
// Two distinct hosting scenarios need two distinct auth registrations. Under IIS in-process
// hosting, IIS's own Windows Authentication module does the actual SSPI handshake and populates
// HttpContext.User before ASP.NET Core ever sees the request — calling AddNegotiate() there throws
// ("The Negotiate Authentication handler cannot be used on a server that directly supports Windows
// Authentication") unless IIS is configured with Windows Auth AND Anonymous Auth both enabled
// simultaneously so the app's own middleware can drive the challenge. That combination reads as an
// anomaly to automated IIS hardening/compliance tooling (e.g. security agents flag "anonymous
// enabled" and silently revert it), so it's not a stable config to depend on in production. Using
// IISDefaults.AuthenticationScheme instead lets IIS run its standard Windows-Auth-only setup
// (Anonymous OFF) with no app-level Negotiate handshake at all. ANCM sets ASPNETCORE_APPL_PATH only
// for in-process IIS hosting, so it's a reliable way to distinguish that from local `dotnet run`
// (plain Kestrel, no IIS integration), which still needs the self-hosted Negotiate handler.
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_APPL_PATH")))
{
    builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
}
else
{
    builder.Services
        .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
        .AddNegotiate();
}

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy; // every endpoint requires an authenticated caller by default
});

builder.Services.Configure<HrGroupOptions>(builder.Configuration.GetSection("HrGroup"));
builder.Services.Configure<AccessOptions>(builder.Configuration.GetSection("Access"));
builder.Services.Configure<AdOptions>(builder.Configuration.GetSection("Ad"));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.AddScoped<IAdDirectoryService, AdDirectoryService>();
builder.Services.AddScoped<RequestAuthorizationService>();
builder.Services.AddScoped<RequestNumberService>();
builder.Services.AddScoped<IAppUserService, AppUserService>();
builder.Services.AddScoped<ID365ApproverService, D365ApproverService>();
builder.Services.AddScoped<ID365ViewerService, D365ViewerService>();
builder.Services.AddScoped<ITicketTemplateService, TicketTemplateService>();
builder.Services.AddScoped<IEmployeeFieldsService, EmployeeFieldsService>();
builder.Services.AddScoped<IRequestTicketService, RequestTicketService>();
builder.Services.AddScoped<ITicketOrchestrationService, TicketOrchestrationService>();
builder.Services.AddScoped<ITicketStatusService, TicketStatusService>();

// --- Ticket-system integrations (submit-time, best-effort — see RequestsController.Submit) ---
builder.Services.Configure<FreshdeskOptions>(builder.Configuration.GetSection("Freshdesk"));
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
builder.Services.Configure<PowerAutomateOptions>(builder.Configuration.GetSection("PowerAutomate"));
builder.Services.Configure<TdxOptions>(builder.Configuration.GetSection("Tdx"));
builder.Services.AddHttpClient<IFreshdeskService, FreshdeskService>();
builder.Services.AddHttpClient<IDynamicsEamService, PowerAutomateEamService>();
builder.Services.AddHttpClient<ITdxService, TdxService>();
builder.Services.AddScoped<IEmailNotificationService, SendGridEmailNotificationService>();

// --- CORS ---
// Frontend and API are hosted as separate top-level IIS sites/ports (not a nested Application —
// see git history for why: Windows Auth on a nested Application produced an unresolved empty 404
// specific to that topology), so this is needed in production too, not just local dev. Windows
// Auth in the browser requires credentials: 'include' on fetch plus an explicit (non-wildcard)
// origin here — AllowCredentials() can't be combined with AllowAnyOrigin().
//
// vm-trm-live:8128 / d365approvals is a SEPARATE standalone app (repo mdeguise/D365Approvals) that
// mirrors this app's "Approbations D365"/"Approbateurs D365" screens but has no backend of its
// own — it calls THIS API directly, cross-origin, so its origin needs to be trusted here too.
const string CorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(
                "http://localhost:5173", "http://vm-trm-live:8090", "http://cycleemploi",
                "http://vm-trm-live:8128", "http://d365approvals")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);

// HTTP-only deployment (no HTTPS binding on the IIS site) — UseHttpsRedirection would just log a
// "failed to determine the https port" warning on every request with no https_port to redirect to.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
