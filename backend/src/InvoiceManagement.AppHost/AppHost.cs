using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// SQL Server
var sqlServer = builder.AddSqlServer("sqlserver", port: 1433)
    .WithDataVolume()
    .AddDatabase("InvoiceManagement");

// Seq (structured logging)
var seq = builder.AddSeq("seq", port: 5341);

// API
builder.AddProject<InvoiceManagement_Api>("api")
    .WithReference(sqlServer)
    .WithReference(seq)
    .WaitFor(sqlServer)
    .WaitFor(seq);

await builder.Build().RunAsync();
