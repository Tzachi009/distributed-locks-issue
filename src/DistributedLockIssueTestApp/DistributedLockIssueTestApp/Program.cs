using DistributedLockIssueTestApp;
using DistributedLockIssueTestApp.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build();

Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .Enrich.FromLogContext()
        .CreateLogger();

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(c => c.ClearProviders())
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration.GetConnectionString("MyDB");

        services.AddDbContextFactory<MyDataContext>(options => options.UseNpgsql(connectionString));
        services.AddHostedService<TestService>();
    })
    .Build()
    .RunAsync();
