using Medallion.Threading;

namespace Sample.App;

public sealed class Job1 : JobBase
{
    public Job1(IServiceScopeFactory serviceScopeFactory, ILogger<Job1> logger)
        : base(serviceScopeFactory, logger) { }
}

public sealed class Job2 : JobBase
{
    public Job2(IServiceScopeFactory serviceScopeFactory, ILogger<Job2> logger)
        : base(serviceScopeFactory, logger) { }
}

public abstract class JobBase : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger _logger;

    public JobBase(IServiceScopeFactory serviceScopeFactory, ILogger logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CancellationTokenRegistration? cancellationTokenRegistration = null;

            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();

                var distributedLockProvider =
                    scope.ServiceProvider.GetRequiredService<IDistributedLockProvider>();

                _logger.LogInformation("Will try to acquire lock.");

                await using var handle = await distributedLockProvider.AcquireLockAsync(
                    "a1416b2940b34bbb9189caaa13f11b1a",
                    cancellationToken: stoppingToken
                );

                _logger.LogInformation(
                    "Acquired {CancelableDescription} lock.",
                    handle.HandleLostToken.CanBeCanceled ? "cancelable" : "uncancelable"
                );

                cancellationTokenRegistration = handle.HandleLostToken.Register(
                    () => _logger.LogError("Lost lock.")
                );

                using var stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken,
                    handle.HandleLostToken
                );

                if (stoppingCts.Token.IsCancellationRequested)
                {
                    return;
                }

                while (!stoppingCts.IsCancellationRequested) // This evaluates to true sometimes even if the database has been restarted
                {
                    _logger.LogInformation("Doing stuff.");

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingCts.Token);
                    }
                    catch (TaskCanceledException) { }

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Cancellation is not requested.");
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Exception {Exception} thrown:\n\r{Message}",
                    exception.GetType(),
                    exception.Message
                );
            }
            finally
            {
                if (cancellationTokenRegistration.HasValue)
                {
                    await cancellationTokenRegistration.Value.DisposeAsync();
                }
            }
        }
    }
}
