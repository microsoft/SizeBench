using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using SizeBench.PathLocators;

namespace SizeBench.LocalBuild;

public sealed class WindsorInstaller : IWindsorInstaller
{
    public void Install(IWindsorContainer container, IConfigurationStore store)
    {
        ArgumentNullException.ThrowIfNull(container);

        container.Register(Component.For<IBinaryLocator>()
                                    .ImplementedBy<LocalBuildPathLocator>()
                                    .LifestyleSingleton());
    }
}
