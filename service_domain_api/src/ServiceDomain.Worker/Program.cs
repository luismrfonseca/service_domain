using Microsoft.EntityFrameworkCore;
using ServiceDomain.Core.Data;
using ServiceDomain.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<ServiceDomainDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LocalDbConnection")));

builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<InboxWorker>();

var host = builder.Build();
host.Run();
