using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[ExcludeFromCodeCoverage]
internal static class TestEnumerationExtensions
{
    internal static IEnumerable<T> EnumerateListButCancelInTheMiddleOfEnumerating<T>(this List<T> list, CancellationTokenSource cts, int cancelAfter)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (i >= cancelAfter)
            {
                cts.Cancel();
            }

            cts.Token.ThrowIfCancellationRequested();
            yield return list[i];
        }
    }

#pragma warning disable IDE0079 // Remove unnecessary suppression - no really, this suppression is necessary.
#pragma warning disable IDE0060 // Remove unused parameter - this parameter is necessary to be an extension method.
    internal static IEnumerable<T> EnumerationThatThrowsIfEverCalled<T>(this List<T> list)
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore IDE0079 // Remove unnecessary suppression
    {
        throw new InvalidOperationException();
        // Yes this says it's unreachable, but it's important because it convinces the compiler to make this into a generator function
        // with a state machine (because of the 'yield return') so that means it doesn't throw immediately upon being called - which is good
        // since the tests want to throw *if* this gets called, so we want to be lazy by design.
#pragma warning disable CS0162 // Unreachable code detected
        yield return default;
#pragma warning restore CS0162 // Unreachable code detected
    }
}
