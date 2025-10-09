var builder = DistributedApplication.CreateBuilder(args);

// Add Python Flask transcript service using Executable resource
var transcriptService = builder.AddExecutable(
    "transcript-service",                                    // Resource name
    "python",                                                 // Command/executable
    "../MAF.InsightStreamer.TranscriptService",              // Working directory
    new[] { "app.py" }                                        // Arguments as array
)
.WithHttpEndpoint(port: 7279, env: "PORT")  // Critical: specify env parameter
.WithEnvironment("FLASK_ENV", "development");

var apiService = builder.AddProject<Projects.MAF_InsightStreamer_Api>("api")
    .WithExternalHttpEndpoints()
    .WithEndpoint("https", endpoint => endpoint.Port = 7276)
    .WithEndpoint("http", endpoint => endpoint.Port = 5232)
    .WithEnvironment("TRANSCRIPTSERVICE_URL", transcriptService.GetEndpoint("http"));  // Pass transcript service URL as environment variable

builder.Build().Run();
