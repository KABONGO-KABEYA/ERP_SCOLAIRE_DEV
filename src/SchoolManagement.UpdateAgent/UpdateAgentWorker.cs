using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SchoolManagement.UpdateAgent;

public sealed class UpdateAgentWorker : BackgroundService
{
    private readonly AgentCycle _cycle;
    private readonly AgentOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<UpdateAgentWorker> _log;

    public UpdateAgentWorker(
        AgentCycle cycle,
        IOptions<AgentOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<UpdateAgentWorker> log)
    {
        _cycle = cycle;
        _options = options.Value;
        _lifetime = lifetime;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "Service {Service} démarré (compte dédié {Account}, pas LocalSystem).",
            AgentServiceNames.WindowsServiceName,
            AgentServiceNames.WindowsAccountName);

        var interval = TimeSpan.FromHours(Math.Max(1, _options.CheckIntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            await _cycle.RunAsync(stoppingToken);
            if (_options.RunOnce)
            {
                _lifetime.StopApplication();
                return;
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
