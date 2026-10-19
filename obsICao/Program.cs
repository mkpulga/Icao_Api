using obsIcaoApi;

Host.CreateDefaultBuilder(args)
    .UseWindowsService() // This is a no-op on Linux/containers
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build()
    .Run();
