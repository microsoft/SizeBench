using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class Session_EnumerateDuplicateDataItemsTests
{
    public TestContext? TestContext { get; set; }

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    public string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory!, binary);

    [TestMethod]
    public async Task CppTestCasesBeforeDuplicateDataCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var duplicates = await session.EnumerateDuplicateDataItems(CancellationToken.None);
        Assert.IsNotNull(duplicates);

        Assert.HasCount(3, duplicates);

        var duplicatePoint = duplicates.Single(dupe => dupe.Symbol.Name == "duplicatedPoint");
        var duplicatePointArray = duplicates.Single(dupe => dupe.Symbol.Name == "duplicatedPointArray");
        var duplicateOnlyInBefore = duplicates.Single(dupe => dupe.Symbol.Name == "duplicatedOnlyInBefore");

        Assert.AreEqual(8u, duplicatePoint.Symbol.Size);
        Assert.AreEqual(24u, duplicatePoint.WastedSize);
        Assert.HasCount(4, duplicatePoint.ReferencedIn);
        Assert.Contains(c => c.ShortName == "SourceFile1.obj", duplicatePoint.ReferencedIn);
        Assert.Contains(c => c.ShortName == "SourceFile2.obj", duplicatePoint.ReferencedIn);
        Assert.Contains(c => c.ShortName == "dllmain.obj", duplicatePoint.ReferencedIn);
        Assert.Contains(c => c.ShortName == "stdafx.obj", duplicatePoint.ReferencedIn);

        Assert.AreEqual(24u, duplicatePointArray.Symbol.Size);
        Assert.AreEqual(72u, duplicatePointArray.WastedSize);
        Assert.HasCount(4, duplicatePointArray.ReferencedIn);
        Assert.Contains(c => c.ShortName == "SourceFile1.obj", duplicatePointArray.ReferencedIn);
        Assert.Contains(c => c.ShortName == "SourceFile2.obj", duplicatePointArray.ReferencedIn);
        Assert.Contains(c => c.ShortName == "dllmain.obj", duplicatePointArray.ReferencedIn);
        Assert.Contains(c => c.ShortName == "stdafx.obj", duplicatePointArray.ReferencedIn);
    }
}
