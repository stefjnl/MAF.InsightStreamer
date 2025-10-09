using MAF.InsightStreamer.Infrastructure.Extensions;
using MAF.InsightStreamer.Infrastructure.Providers;

var builder = WebApplication.CreateBuilder(args);

// Add .NET Aspire service defaults
builder.AddServiceDefaults();

// Bind configuration using Options pattern
builder.Services.Configure<ProviderSettings>(
    builder.Configuration.GetSection(ProviderSettings.SectionName));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Use CORS before other middleware
app.UseCors("AllowAll");

app.UseHttpsRedirection();

// Enable static files serving - order matters!
app.UseDefaultFiles();  // This should come before UseStaticFiles
app.UseStaticFiles();

app.MapControllers();

// Map default endpoints for Aspire dashboard
app.MapDefaultEndpoints();

app.Run();

public partial class Program { }