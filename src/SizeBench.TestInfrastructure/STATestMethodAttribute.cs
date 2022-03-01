namespace SizeBench.TestInfrastructure;

// Copied from https://github.com/microsoft/PowerToys/blob/master/src/modules/previewpane/STATestMethodAttribute.cs
public sealed class STATestMethodAttribute : TestMethodAttribute
{
    public TestMethodAttribute? TestMethodAttribute { get; }

    public STATestMethodAttribute()
    {
    }

    public STATestMethodAttribute(TestMethodAttribute testMethodAttribute)
    {
        this.TestMethodAttribute = testMethodAttribute;
    }

    public override TestResult[] Execute(ITestMethod testMethod)
    {
        ArgumentNullException.ThrowIfNull(testMethod);

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return Invoke(testMethod);
        }

        var result = Array.Empty<TestResult>();
        var thread = new Thread(() => result = Invoke(testMethod));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    private TestResult[] Invoke(ITestMethod testMethod)
    {
        if (this.TestMethodAttribute != null)
        {
            return this.TestMethodAttribute.Execute(testMethod);
        }

        return new[] { testMethod.Invoke(null) };
    }
}
