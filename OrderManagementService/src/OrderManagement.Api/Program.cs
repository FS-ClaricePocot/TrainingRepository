using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using OrderManagement.Api.Auth;
using OrderManagement.Api.RateLimiting;
using OrderManagement.Api.Reports;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Adding authentication schemes for cookie and API key
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "Selector";
    })
    .AddPolicyScheme("Selector", "Cookie or ApiKey", options =>
    {
        options.ForwardDefaultSelector = AuthSchemeSelector.Select;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        //TODO: configurations for cookie name, expiry, etc. for internal admin tool
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };

    })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthConstants.SchemeName, options =>
    {
        //TODO: configurations for external partner apikey here
    });


builder.Services.AddOrderApiAuthorization();
builder.Services.AddOrderApiRateLimiting(builder.Configuration);


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

var connectionString = builder.Configuration.GetConnectionString("OrderManagementDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:OrderManagementDb.");

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(sp => new OrderService(connectionString, sp.GetRequiredService<IMemoryCache>()));
builder.Services.AddSingleton<ReportJobQueue>();
builder.Services.AddSingleton(sp => new ReportService(connectionString, sp.GetRequiredService<IMemoryCache>(), sp.GetRequiredService<ReportJobQueue>()));
builder.Services.AddHostedService<ReportGenerationWorker>();


if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyMethod()
    .AllowAnyHeader()));
}
else
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(p => p
    .WithMethods("GET")
    .WithHeaders("Content-Type", "Authorization")));
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();
