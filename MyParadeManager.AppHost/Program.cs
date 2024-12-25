using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<MyParadeManager_WebApp>("app");

builder.Build().Run();