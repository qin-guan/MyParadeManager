using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("db").AddDatabase("MyParadeManager");
builder.AddProject<MyParadeManager_WebApp>("app")
    .WithReference(db);

builder.Build().Run();