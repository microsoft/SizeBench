namespace SizeBench.TestInfrastructure;

// Copied from https://github.com/microsoft/PowerToys/blob/master/src/modules/previewpane/STATestClassAttribute.cs
public sealed class STATestClassAttribute : TestClassAttribute
{
    public override TestMethodAttribute? GetTestMethodAttribute(TestMethodAttribute? testMethodAttribute)
    {
        if (testMethodAttribute is STATestMethodAttribute)
        {
            return testMethodAttribute;
        }

        if (testMethodAttribute is not null)
        {
            return new STATestMethodAttribute(base.GetTestMethodAttribute(testMethodAttribute)!);
        }

        return null;
    }
}
