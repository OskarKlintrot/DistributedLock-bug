using Medallion.Threading;
using Medallion.Threading.SqlServer;
using Microsoft.EntityFrameworkCore;
using Sample.App;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(
        builder =>
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "hh:mm:ss ";
            })
    )
    .ConfigureServices(
        (context, services) =>
        {
            var configuration = context.Configuration;

            services
                .AddApplicationInsightsTelemetryWorkerService(options =>
                {
                    options.EnableAdaptiveSampling = false;
                    options.EnablePerformanceCounterCollectionModule = false;
                    options.EnableEventCounterCollectionModule = false;
                })
                .AddDbContext<SampleDbContext>(
                    (_, options) =>
                    {
                        options.UseSqlServer(
                            connectionString: configuration.GetConnectionString("DbConnection")
                        );
                    }
                )
                .AddSingleton<IDistributedLockProvider>(
                    provider =>
                        new SqlDistributedSynchronizationProvider(
                            provider
                                .GetRequiredService<IConfiguration>()
                                .GetConnectionString("DbConnection")
                        )
                )
                .AddHostedService<Job1>()
                .AddHostedService<Job2>();
        }
    )
    .Build();

await using (
    var scope = host.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope()
)
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

    dbContext.Database.Migrate();
}

await host.RunAsync();
