using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Add a secret parameter for the SQL password
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// Pass the password parameter to the SQL Server resource
var sqlServer = builder.AddSqlServer("sqlserver", password: sqlPassword, port: 1433)
    .WithDataVolume()
    .WithDbGate()
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
