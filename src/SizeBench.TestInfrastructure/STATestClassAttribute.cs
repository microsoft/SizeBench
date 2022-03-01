namespace SizeBench.TestInfrastructure;

// Copied from https://github.com/microsoft/PowerToys/blob/master/src/modules/previewpane/STATestClassAttribute.cs
public sealed class STATestClassAttribute : TestClassAttribute
{
    public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
    {
        if (testMethodAttribute is STATestMethodAttribute)
        {
            return testMethodAttribute;
        }

        return new STATestMethodAttribute(base.GetTestMethodAttribute(testMethodAttribute));
    }
}
