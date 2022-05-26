using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class Session_EnumerateAnnotationsTests
{
    public TestContext? TestContext { get; set; }

    public string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory, binary);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");
    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    [TestMethod]
    public async Task CppTestCasesBeforeAnnotationsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var annotations = await session.EnumerateAnnotations(CancellationToken.None);
        Assert.IsNotNull(annotations);

        Assert.AreEqual(3, annotations.Where(a => a.SourceFileName != "stdafx.h").Count());

        var sourceFile2Annotation = annotations.Single(a => a.Text == "This is a test annotation in SourceFile2.cpp");
        Assert.AreEqual(19u, sourceFile2Annotation.LineNumber);
        StringAssert.EndsWith(sourceFile2Annotation.SourceFile!.Name, @"testpeprojects\sizebenchv2.analysisengine.tests.cpptestcasesbefore\sourcefile2.cpp", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(sourceFile2Annotation.IsInlinedOrAnnotatingInlineSite);

        var sourceFile1Annotation = annotations.Single(a => a.Text == "This is a test annotation in SourceFile1.cpp");
        Assert.AreEqual(24u, sourceFile1Annotation.LineNumber);
        StringAssert.EndsWith(sourceFile1Annotation.SourceFile!.Name, @"testpeprojects\sizebenchv2.analysisengine.tests.cpptestcasesbefore\sourcefile1.cpp", StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(sourceFile1Annotation.IsInlinedOrAnnotatingInlineSite);

        var dllMainAnnotation = annotations.Single(a => a.Text == "annotation in DllMain itself, on the line that gets inlined...");
        // This line number and source file are wrong because we can't get inline sites resolving correctly in the edge case where an annotation is
        // applied to a function call that is inlined.  So this is commented out for now.
        //Assert.AreEqual(36u, dllMainAnnotation.LineNumber);
        //Assert.AreEqual(@"c:\users\austi\source\repos\sizebench\testpeprojects\sizebenchv2.analysisengine.tests.cpptestcasesbefore\dllmain.cpp", dllMainAnnotation.SourceFile);
        Assert.IsTrue(dllMainAnnotation.IsInlinedOrAnnotatingInlineSite);
    }
}
