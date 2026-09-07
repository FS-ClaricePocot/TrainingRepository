using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using OrderManagement.Api.Auth;

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
    })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthConstants.SchemeName, options => 
    { 
        //TODO: configurations for external partner apikey here
    });


builder.Services.AddOrderApiAuthorization();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

var connectionString = builder.Configuration.GetConnectionString("OrderManagementDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:OrderManagementDb.");

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(sp => new OrderService(connectionString, sp.GetRequiredService<IMemoryCache>()));


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

app.MapControllers();

app.Run();
