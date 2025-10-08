var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MAF_InsightStreamer_Api>("api");

builder.Build().Run();
