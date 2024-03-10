namespace DoMyThingWorker.BGServices;

public class VFSInspector : IHostedService, IDisposable
{
    private int executionCount = 0;
    private readonly ILogger<VFSInspector> _logger;
    private readonly IConfiguration _configuration;
    private Timer? _timer = null;

    public VFSInspector(ILogger<VFSInspector> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timed Hosted Service running.");

        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        var count = Interlocked.Increment(ref executionCount);

        _logger.LogInformation(
            "Timed Hosted Service is working. Count: {Count}", count);
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timed Hosted Service is stopping.");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}