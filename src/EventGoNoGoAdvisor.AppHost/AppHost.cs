var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.EventGoNoGoAdvisor_Api>("event-advisor-api")
    // Only affects `aspire publish` / manifest generation, not `dotnet run`.
    // Aspire's default context path for a project resource is the project's
    // own directory, which breaks our Dockerfile's COPY of the sibling
    // ServiceDefaults project — repoint the build at the repo root instead.
    .PublishAsDockerFile(container => container.WithDockerfile(
        contextPath: "../..",
        dockerfilePath: "src/EventGoNoGoAdvisor.Api/Dockerfile"));

builder.Build().Run();
