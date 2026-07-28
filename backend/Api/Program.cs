using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
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
// Microsoft.Identity.Web validates the incoming bearer token against Entra ID, and provides
// ITokenAcquisition for the on-behalf-of Graph call GraphGroupService uses for the HR group check.
builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization();

builder.Services.Configure<HrGroupOptions>(builder.Configuration.GetSection("HrGroup"));
builder.Services.AddHttpClient("Graph");
builder.Services.AddScoped<IGraphGroupService, GraphGroupService>();
builder.Services.AddScoped<RequestAuthorizationService>();
builder.Services.AddScoped<RequestNumberService>();

// --- CORS ---
// Only needed for local dev (frontend on :5173, backend on a different port). In Docker/prod, the
// web container's Nginx reverse-proxies /api/* to the api container, so browser requests are
// same-origin and this policy is unused there.
const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCorsPolicy);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
