namespace ProgressReportingDemo.Services;

public partial class LongRunningTaskProgressService
{
    private int _percentage = 0;
    private readonly Random _random = new();
    private readonly ILogger<LongRunningTaskProgressService> _logger;
    private Lock _lock = new();
    private DateTimeOffset _nextUpdate = DateTimeOffset.Now;

    public LongRunningTaskProgressService(ILogger<LongRunningTaskProgressService> logger)
    {
        _logger = logger;
    }

    public ProgressReport GetProgress()
    {
        _logger.LogInformation("Getting progress");

        lock (_lock)
        {
            if (DateTimeOffset.Now > _nextUpdate)
            {
                _logger.LogInformation("Updating progress");
                _percentage += 1;

                // Reset the percentage to 0 when it reaches 100
                if (_percentage >= 100)
                {
                    _percentage = 0;
                }

                _nextUpdate = DateTimeOffset.Now.AddSeconds((double)_random.Next(1, 10) / 10);
            }
            return new ProgressReport(_percentage, DateTimeOffset.Now);
        }
    }

    public async Task MonitorProgress(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monitoring progress");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500);
            progress.Report(GetProgress());
        }

        _logger.LogInformation("Monitoring progress cancelled");
    }
}