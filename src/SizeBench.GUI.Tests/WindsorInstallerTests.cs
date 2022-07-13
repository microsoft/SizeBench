using Castle.MicroKernel.Registration;
using Castle.Windsor;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.Logging;
using SizeBench.GUI.Pages;
using SizeBench.TestInfrastructure;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Tests;

[TestClass]
public sealed class WindsorInstallerTests : IDisposable
{
    public IWindsorContainer WindsorContainer = new WindsorContainer();

    [TestInitialize]
    public void TestInitialize()
    {
        this.WindsorContainer = new WindsorContainer();
        this.WindsorContainer.Install(new WindsorInstaller());
    }

    [TestMethod]
    public void ViewModelsOtherThanMainWindowAreTransient()
    {
        using var appLogger = new TestNoOpApplicationLogger();
        this.WindsorContainer.Register(Component.For<IApplicationLogger>()
                                                .Instance(appLogger));
        this.WindsorContainer.Register(Component.For<IExcelExporter>()
                                                .Instance(new Mock<IExcelExporter>().Object));
        this.WindsorContainer.Register(Component.For<IUITaskScheduler>()
                                                .Instance(new Mock<IUITaskScheduler>().Object));
        this.WindsorContainer.Register(Component.For<ISession>()
                                                .Instance(new Mock<ISession>().Object));

        var viewModel1 = this.WindsorContainer.Resolve<AllBinarySectionsPageViewModel>();
        var viewModel2 = this.WindsorContainer.Resolve<AllBinarySectionsPageViewModel>();
        Assert.IsFalse(ReferenceEquals(viewModel1, viewModel2));
    }

    public void Dispose() => this.WindsorContainer.Dispose();
}
