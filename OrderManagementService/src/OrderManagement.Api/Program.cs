var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

var connectionString = builder.Configuration.GetConnectionString("OrderManagementDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:OrderManagementDb.");

builder.Services.AddSingleton(new OrderService(connectionString));


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

app.UseAuthorization();

app.MapControllers();

app.Run();
