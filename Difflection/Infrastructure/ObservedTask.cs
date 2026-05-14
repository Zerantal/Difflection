using System;
using System.Threading.Tasks;

namespace Difflection.Infrastructure;

public static class ObservedTask
{
    public static async Task ReportFailureAsync(Task task, string message)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            ApplicationErrorReporter.Report(exception, message);
        }
    }
}
