var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.EventGoNoGoAdvisor_Api>("event-advisor-api");

builder.Build().Run();
