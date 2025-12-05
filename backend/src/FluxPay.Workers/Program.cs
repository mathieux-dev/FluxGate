using FluxPay.Infrastructure;
using FluxPay.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReconciliationWorker>();

var host = builder.Build();
host.Run();
