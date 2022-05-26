using System.Reflection;

namespace SizeBench.ErrorReporting.Tests;

[TestClass]
public sealed class TestingTheTests
{
    // This is a protection against some mistakes made in testing in the past where test classes were marked as "internal"
    // which silently prevents them from running in MSTest.  It's never expected that someone would go to the work of writing a test class only
    // to have it not run - so if we find any tests marked as internal, fail this test to signal that there's problems elsewhere.

    [TestMethod]
    public void InternalTestClassesShouldNotExist()
    {
        var allTypes = typeof(TestingTheTests).Assembly.GetTypes();
        foreach (var type in allTypes)
        {
            if (type.GetCustomAttribute<TestClassAttribute>() != null)
            {
                if (type.IsNotPublic)
                {
                    Assert.Fail($"The class {type.Name} is not public, but it is marked as [TestClass].  This prevents MSTest from running the tests inside it, and you surely didn't mean to do that - make the type public.");
                }
            }
        }
    }
}
