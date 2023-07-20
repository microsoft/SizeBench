using System.IO;
using System.Windows;
using System.Windows.Controls;
using SizeBench.AnalysisEngine;
using SizeBench.Logging;
using SizeBench.TestInfrastructure;

namespace SizeBench.GUI.Controls.TypeLayoutTreeView.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb")]
[STATestClass]
public class TypeLayoutDataTemplateSelectorTests
{
    public FrameworkElement? TestElement { get; private set; }
    public DataTemplate? PlaceholderTemplate { get; private set; }
    public DataTemplate? TypeTemplate { get; private set; }
    public DataTemplate? SimpleMemberTemplate { get; private set; }
    public DataTemplate? ComplexMemberTemplate { get; private set; }

    [TestInitialize]
    public void TestInitialize()
    {
        this.PlaceholderTemplate = new DataTemplate();
        this.TypeTemplate = new HierarchicalDataTemplate();
        this.SimpleMemberTemplate = new DataTemplate();
        this.ComplexMemberTemplate = new HierarchicalDataTemplate();

        this.TestElement = new Button()
        {
            Resources = new ResourceDictionary()
        };

        this.TestElement.Resources.Add("LoadingItemTemplate", this.PlaceholderTemplate);
        this.TestElement.Resources.Add("ClassTemplate", this.TypeTemplate);
        this.TestElement.Resources.Add("MemberTemplate", this.SimpleMemberTemplate);
        this.TestElement.Resources.Add("MemberWithLinkedTypeTemplate", this.ComplexMemberTemplate);
    }

    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BeforeBinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");
    private string BeforePDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");
    private string AfterBinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll");
    private string AfterPDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb");

    [TestMethod]
    public void BadInputsReturnsNull()
    {
        Assert.IsNull(TypeLayoutDataTemplateSelector.Instance.SelectTemplate(null, this.TestElement!));
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.  This test is explicitly checking null
        Assert.IsNull(TypeLayoutDataTemplateSelector.Instance.SelectTemplate(TypeLayoutItemViewModel.PlaceholderLoadingItem, null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.IsNull(TypeLayoutDataTemplateSelector.Instance.SelectTemplate(123, this.TestElement!));
    }

    //----------------------------------------------------------------------------------------------------
    // 
    // Tests for a single binary
    //
    //----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void PlaceholderLoadingItemTemplateFoundCorrectly()
    {
        var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(TypeLayoutItemViewModel.PlaceholderLoadingItem, this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.PlaceholderTemplate));
    }

    [TestMethod]
    public async Task NonPlaceholderTypeLayoutItemTemplateFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BeforeBinaryPath, this.BeforePDBPath, logger);
        var layouts = await session.LoadTypeLayoutsByName("_XSTATE_CONFIGURATIONTest", CancellationToken.None);
        var layoutItem = new TypeLayoutItemViewModel(layouts[0], session, true);
        var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(layoutItem, this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.TypeTemplate));
    }

    [TestMethod]
    public async Task SimpleMemberLayoutTemplateFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BeforeBinaryPath, this.BeforePDBPath, logger);
        var layouts = await session.LoadTypeLayoutsByName("_XSTATE_CONFIGURATIONTest", CancellationToken.None);
        var layoutItem = new TypeLayoutItemViewModel(layouts[0], session, true);
        var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(layoutItem.Members[0], this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.SimpleMemberTemplate));
    }

    [TestMethod]
    public async Task ComplexMemberLayoutTemplateFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BeforeBinaryPath, this.BeforePDBPath, logger);
        var layouts = await session.LoadTypeLayoutsByName("_XSTATE_CONFIGURATIONTest", CancellationToken.None);
        var layoutItem = new TypeLayoutItemViewModel(layouts[0], session, true);
        var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(layoutItem.Members[6], this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.ComplexMemberTemplate));
    }

    [TestMethod]
    public async Task MembersWithModifiedTypesCanFindTemplateCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BeforeBinaryPath, this.BeforePDBPath, logger);
        var layouts = await session.LoadTypeLayoutsByName("TestMemberTypes", CancellationToken.None);
        var layoutItem = new TypeLayoutItemViewModel(layouts[0], session, true);
        // Every member should be able to find its resulting template, even though there are modified types, arrays, pointers, and combinations
        foreach (var member in layoutItem.Members)
        {
            var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(member, this.TestElement!);
            Assert.IsTrue(ReferenceEquals(result, this.SimpleMemberTemplate));
        }
    }

    //----------------------------------------------------------------------------------------------------
    // 
    // Tests for a diff
    //
    //----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void DiffPlaceholderLoadingItemTemplateFoundCorrectly()
    {
        var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(TypeLayoutItemDiffViewModel.PlaceholderLoadingItem, this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.PlaceholderTemplate));
    }

    [TestMethod]
    public async Task DiffNonPlaceholderTypeLayoutItemTemplateFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var layouts = await diffSession.LoadTypeLayoutDiffsByName("TypeLayoutDiff_Basics", CancellationToken.None);
        var layoutItem = new TypeLayoutItemDiffViewModel(layouts[0], diffSession);
        var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(layoutItem, this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.TypeTemplate));
    }

    [TestMethod]
    public async Task DiffSimpleMemberLayoutTemplateFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var layouts = await diffSession.LoadTypeLayoutDiffsByName("TypeLayoutDiff_Basics", CancellationToken.None);
        var layoutItem = new TypeLayoutItemDiffViewModel(layouts[0], diffSession);
        var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(layoutItem.Members[0], this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.SimpleMemberTemplate));
    }

    [TestMethod]
    public async Task DiffComplexMemberLayoutTemplateFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var layouts = await diffSession.LoadTypeLayoutDiffsByName("_XSTATE_CONFIGURATIONTest", CancellationToken.None);
        var layoutItem = new TypeLayoutItemDiffViewModel(layouts[0], diffSession);
        var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(layoutItem.Members[6], this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.ComplexMemberTemplate));
    }

    [TestMethod]
    public async Task DiffMembersWithModifiedTypesCanFindTemplateCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var layouts = await diffSession.LoadTypeLayoutDiffsByName("TestMemberTypes", CancellationToken.None);
        var layoutItem = new TypeLayoutItemDiffViewModel(layouts[0], diffSession);
        // Every member should be able to find its resulting template, even though there are modified types, arrays, pointers, and combinations
        foreach (var member in layoutItem.Members)
        {
            var result = TypeLayoutDataTemplateSelector.Instance.SelectTemplate(member, this.TestElement!);
            Assert.IsTrue(ReferenceEquals(result, this.SimpleMemberTemplate));
        }
    }
}
