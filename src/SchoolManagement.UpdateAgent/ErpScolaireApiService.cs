using System.ServiceProcess;

namespace SchoolManagement.UpdateAgent;

public interface IApiWindowsService
{
    string ServiceName { get; }

    Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken);

    Task StartAsync(TimeSpan timeout, CancellationToken cancellationToken);

    Task<bool> IsRunningAsync(CancellationToken cancellationToken);
}

public sealed class ErpScolaireApiService : IApiWindowsService
{
    public string ServiceName => AgentServiceNames.ApiWindowsServiceName;

    public Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.Run(() => Control(start: false, timeout), cancellationToken);

    public Task StartAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.Run(() => Control(start: true, timeout), cancellationToken);

    public Task<bool> IsRunningAsync(CancellationToken cancellationToken)
    {
        using var sc = new ServiceController(ServiceName);
        return Task.FromResult(sc.Status == ServiceControllerStatus.Running);
    }

    private void Control(bool start, TimeSpan timeout)
    {
        using var sc = new ServiceController(ServiceName);
        if (start)
        {
            if (sc.Status == ServiceControllerStatus.Running)
            {
                return;
            }

            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
            return;
        }

        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            return;
        }

        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
    }
}
