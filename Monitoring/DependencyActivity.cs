using System.Diagnostics;

namespace Monitoring;

public static class DependencyActivity
{
    public static async Task<T> TraceAsync<T>(
        string activityName,
        string dependencyName,
        string system,
        string operation,
        Func<Task<T>> action)
    {
        using var activity = Start(
            activityName,
            dependencyName,
            system,
            operation);

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public static async Task TraceAsync(
        string activityName,
        string dependencyName,
        string system,
        string operation,
        Func<Task> action)
    {
        using var activity = Start(
            activityName,
            dependencyName,
            system,
            operation);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private static Activity? Start(
        string activityName,
        string dependencyName,
        string system,
        string operation)
    {
        var activity = MonitorService.ActivitySource.StartActivity(
            activityName,
            ActivityKind.Client);

        activity?.SetTag("dependency.name", dependencyName);
        activity?.SetTag("db.system", system);
        activity?.SetTag("db.operation", operation);

        return activity;
    }
}
