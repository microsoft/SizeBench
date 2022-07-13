using System.Diagnostics.CodeAnalysis;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Tests;

[ExcludeFromCodeCoverage]
internal static class MockUITaskSchedulerExtensions
{
    public static void SetupForSynchronousCompletionOfLongRunningUITasks(this Mock<IUITaskScheduler> mockScheduler)
    {
        // Synchronously complete any task given to us
        mockScheduler.Setup(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()))
                     .Returns<string, Func<CancellationToken, Task>>(
            async (taskName, func) =>
            {
                try
                {
                    await func(CancellationToken.None);
                }
                catch (OperationCanceledException) { } // Cancellation is not failure, we'll swallow
                catch (AggregateException aggEx) when (aggEx.InnerException is OperationCanceledException) { }
            });
    }
}
