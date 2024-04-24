using CRM_Integration_service;
using o2g.Utility;

HttpClientBuilder.DisableSSValidation = true;
HttpClientBuilder.TraceREST = true;
IHost host = Host.CreateDefaultBuilder(args)

    .ConfigureServices(services =>
    {
      services.AddHostedService<Worker>();
      services.AddHttpClient();
    })
    .UseWindowsService()
    .Build();

host.Run();
