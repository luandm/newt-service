using NewtService.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<NewtWorker>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = NewtService.Core.ServiceConstants.ServiceName;
});

var host = builder.Build();
host.Run();
