using DoMyThingWorker.Models;
using DoMyThingWorker.Processors;

namespace DoMyThingWorker.BGServices;

public class VFSInspector : IHostedService, IDisposable
{
    private int executionCount = 0;
    private readonly ILogger<VFSInspector> _logger;
    private readonly IConfiguration _configuration;
    private readonly IProcessor<FindVFSAppointmentModel,FindVFSAppointmentResponseModel> _processor;
    private Timer? _timer = null;

    public VFSInspector(ILogger<VFSInspector> logger, IConfiguration configuration, IProcessor<FindVFSAppointmentModel,FindVFSAppointmentResponseModel> processor)
    {
        _logger = logger;
        _configuration = configuration;
        _processor = processor;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timed Hosted Service running.");

        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5000));

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        try
        {
            var count = Interlocked.Increment(ref executionCount);
            _processor.ProcessAsync(new());
            _logger.LogInformation(
                "Timed Hosted Service is working. Count: {Count}", count);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
       
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