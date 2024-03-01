namespace SizeBench.TestInfrastructure;
public static class CommonTestCategories
{
    // If you change these strings or add new ones, consider updating the test *.playlist files so the Visual Studio Test Explorer
    // has a good balance between what to run locally (or in Live Unit Testing) vs. the expanded set to run in the pipelines.
    public const string SlowTests = "SlowTests";
}
