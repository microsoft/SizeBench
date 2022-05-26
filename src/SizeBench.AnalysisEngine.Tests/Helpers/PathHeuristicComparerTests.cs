using SizeBench.AnalysisEngine.Helpers;

namespace SizeBench.AnalysisEngine.Tests;

[TestClass]
public class PathHeuristicComparerTests
{
    [TestMethod]
    public void PathNamesWithDifferentEnlistmentRootsStillConsideredSimilar()
    {
        Assert.IsTrue(PathHeuristicComparer.PathNamesAreVerySimilar(@"p:\os\src\mylib.lib",
                                                                    @"w:\dd\src\mylib.lib"));
        Assert.IsTrue(PathHeuristicComparer.PathNamesAreVerySimilar(@"p:\os\src\folder1\folder2\foo.obj",
                                                                    @"w:\dd\root2\src\folder1\folder2\foo.obj"));
    }

    [TestMethod]
    public void PathNamesWithDifferentFilenamesAreNotConsideredSimilar()
    {
        Assert.IsFalse(PathHeuristicComparer.PathNamesAreVerySimilar(@"p:\os\src\mylib.lib",
                                                                     @"p:\os\src\mylii.lib"));
        Assert.IsFalse(PathHeuristicComparer.PathNamesAreVerySimilar(@"p:\os\src\folder1\foo.obj",
                                                                     @"p:\os\src\folder1\fo0.obj"));
    }

    [TestMethod]
    public void PathNamesThatAreDifferentAreNotConsideredSimilarEvenWithSameFilename()
    {
        Assert.IsFalse(PathHeuristicComparer.PathNamesAreVerySimilar(@"p:\os\src\mylib.lib",
                                                                     @"p:\os\src\folder2\mylib.lib"));
    }
}
