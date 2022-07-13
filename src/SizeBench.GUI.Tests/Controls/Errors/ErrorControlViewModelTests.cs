using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SizeBench.AnalysisEngine;
using SizeBench.ErrorReporting;
using SizeBench.Logging;

namespace SizeBench.GUI.Controls.Errors.Tests;

[TestClass]
public sealed class ErrorControlViewModelTests
{
    [TestMethod]
    public void ErrorContextExtracted()
    {
        var mockSession1 = new Mock<ISession>();
        mockSession1.Setup(s => s.BinaryPath).Returns(@"c:\test\session1.dll");
        mockSession1.Setup(s => s.PdbPath).Returns(@"c:\test\session1.pdb");
        var mockSession2 = new Mock<ISession>();
        mockSession2.Setup(s => s.BinaryPath).Returns(@"c:\test\session2.exe");
        mockSession2.Setup(s => s.PdbPath).Returns(@"c:\test\session2.pdb");

        var openSessions = new List<ISession>()
            {
                mockSession1.Object,
                mockSession2.Object,
            };

        var mockDiffBefore1 = new Mock<ISession>();
        mockDiffBefore1.Setup(s => s.BinaryPath).Returns(@"c:\test\diffs\before\1.dll");
        mockDiffBefore1.Setup(s => s.PdbPath).Returns(@"c:\test\diffs\before\1.pdb");
        var mockDiffAfter1 = new Mock<ISession>();
        mockDiffAfter1.Setup(s => s.BinaryPath).Returns(@"c:\test\diffs\after\1.dll");
        mockDiffAfter1.Setup(s => s.PdbPath).Returns(@"c:\test\diffs\after\1.pdb");
        var mockDiffSession1 = new Mock<IDiffSession>();
        mockDiffSession1.Setup(diff => diff.BeforeSession).Returns(mockDiffBefore1.Object);
        mockDiffSession1.Setup(diff => diff.AfterSession).Returns(mockDiffAfter1.Object);

        var mockDiffBefore2 = new Mock<ISession>();
        mockDiffBefore2.Setup(s => s.BinaryPath).Returns(@"c:\test\diffs\2before.dll");
        mockDiffBefore2.Setup(s => s.PdbPath).Returns(@"c:\test\diffs\2before.pdb");
        var mockDiffAfter2 = new Mock<ISession>();
        mockDiffAfter2.Setup(s => s.BinaryPath).Returns(@"c:\test\diffs\2after.dll");
        mockDiffAfter2.Setup(s => s.PdbPath).Returns(@"c:\test\diffs\2after.pdb");
        var mockDiffSession2 = new Mock<IDiffSession>();
        mockDiffSession2.Setup(diff => diff.BeforeSession).Returns(mockDiffBefore2.Object);
        mockDiffSession2.Setup(diff => diff.AfterSession).Returns(mockDiffAfter2.Object);

        var openDiffSessions = new List<IDiffSession>()
            {
                mockDiffSession1.Object,
                mockDiffSession2.Object,
            };

        var mockSessionFactory = new Mock<ISessionFactory>();
        mockSessionFactory.Setup(sf => sf.OpenSessions).Returns(openSessions);
        mockSessionFactory.Setup(sf => sf.OpenDiffSessions).Returns(openDiffSessions);

        var applicationLogger = new ApplicationLogger("Test app name", null);

        var innerExceptionMessage = "inner exception for testing";
        var outerExceptionMessage = "This is a test outer exception";

        Exception testException;
        try
        {
            try
            {
                throw new ArithmeticException(innerExceptionMessage);
            }
            catch (Exception innerException)
            {
                throw new InvalidOperationException(outerExceptionMessage, innerException);
            }
        }
        catch (Exception ex)
        {
            testException = ex;
        }

        var leadingText = "Some leading text goes here\nwith newlines no less!";
        var vm = new ErrorControlViewModel(testException, applicationLogger, mockSessionFactory.Object, leadingText);

        Assert.AreEqual(leadingText, vm.LeadingText);

        // The log path should be temporary with a suffix that suggests its purpose
        StringAssert.Contains(vm.LogFilePath, Path.GetTempPath(), StringComparison.Ordinal);
        StringAssert.EndsWith(vm.LogFilePath, ".sizebenchlog.txt", StringComparison.Ordinal);

        var openFilePathsAsList = vm.OpenFilePaths.ToList();
        foreach (var session in openSessions)
        {
            CollectionAssert.Contains(openFilePathsAsList, session.BinaryPath);
            CollectionAssert.Contains(openFilePathsAsList, session.PdbPath);
        }

        foreach (var diffSession in openDiffSessions)
        {
            CollectionAssert.Contains(openFilePathsAsList, diffSession.BeforeSession.BinaryPath);
            CollectionAssert.Contains(openFilePathsAsList, diffSession.BeforeSession.PdbPath);
            CollectionAssert.Contains(openFilePathsAsList, diffSession.AfterSession.BinaryPath);
            CollectionAssert.Contains(openFilePathsAsList, diffSession.AfterSession.PdbPath);
        }

        // Error Details contains the exception data including stack trace info
        var testExceptionStackTrace = new StackTrace(testException);
        StringAssert.Contains(vm.ErrorDetails, innerExceptionMessage, StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, outerExceptionMessage, StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, testExceptionStackTrace.GetFrame(0)!.GetMethod()!.Name, StringComparison.Ordinal);

        // Error Details also contains information about the process
        StringAssert.Contains(vm.ErrorDetails, Environment.CommandLine, StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, Process.GetCurrentProcess().ProcessName, StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, $"64-bit Process: {Environment.Is64BitProcess}", StringComparison.Ordinal);

        // And it contains information about the environment
        StringAssert.Contains(vm.ErrorDetails, RuntimeInformation.FrameworkDescription, StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, RuntimeInformation.OSDescription, StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, $"OS Architecture: {RuntimeInformation.OSArchitecture}", StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, $"Process Architecture: {RuntimeInformation.ProcessArchitecture}", StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, $"Locale: {CultureInfo.CurrentCulture}", StringComparison.Ordinal);
        StringAssert.Contains(vm.ErrorDetails, $"UI Locale: {CultureInfo.CurrentUICulture}", StringComparison.Ordinal);

        // The e-mail link contains the right address, subject line, and body
        StringAssert.StartsWith(vm.EmailLink, "mailto:sizebenchcrash@microsoft.com", StringComparison.Ordinal);
        StringAssert.Contains(vm.EmailLink, $"Subject={Uri.EscapeDataString($"SizeBench error - {testException.Hash()}")}", StringComparison.Ordinal);
        StringAssert.Contains(vm.EmailLink, $"Body={Uri.EscapeDataString(vm.ErrorDetails)}", StringComparison.Ordinal);
    }
}
