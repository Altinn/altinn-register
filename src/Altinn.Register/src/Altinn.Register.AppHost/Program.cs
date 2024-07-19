var builder = DistributedApplication.CreateBuilder(args);

var registerApi = builder.AddProject<Projects.Altinn_Register>("register");

builder.Build().Run();
