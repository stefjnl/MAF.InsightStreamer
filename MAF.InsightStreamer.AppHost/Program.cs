var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.MAF_InsightStreamer_Api>("api")
    .WithExternalHttpEndpoints()
    .WithEndpoint("https", endpoint => endpoint.Port = 7276)
    .WithEndpoint("http", endpoint => endpoint.Port = 5232);

builder.Build().Run();
