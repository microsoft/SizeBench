using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;

namespace SizeBench.ExcelExporter;

public sealed class WindsorInstaller : IWindsorInstaller
{
    public void Install(IWindsorContainer container, IConfigurationStore store)
    {
        ArgumentNullException.ThrowIfNull(container);

        container.Register(Component.For<IExcelExporter>()
                                    .ImplementedBy<ClosedXMLExporter>()
                                    .LifestyleSingleton());
    }
}
