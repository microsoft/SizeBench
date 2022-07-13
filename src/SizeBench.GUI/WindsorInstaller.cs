using System.Windows;
using Castle.MicroKernel.ModelBuilder.Inspectors;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using SizeBench.Logging;

namespace SizeBench.GUI;

public class WindsorInstaller : IWindsorInstaller
{
    public void Install(IWindsorContainer container, IConfigurationStore store)
    {
        ArgumentNullException.ThrowIfNull(container);

        container.Register(Component.For<IApplicationLogger>()
#pragma warning disable CA2000 // Dispose objects before losing scope - the ApplicationLogger is meant to live as long as the app.
                     .Instance(new ApplicationLogger("SizeBench App", SynchronizationContext.Current)));
#pragma warning restore CA2000 // Dispose objects before losing scope

        // We don't want to inject properties, only ctors
        // One example of a reason to avoid this WPF Window objects have an "Owner" property which is settable
        // but we don't want to (in fact, can't) set this before the window is Shown.  If Windsor tries to set it,
        // it throws an exception and that doesn't help anybody.
        // Plus, in SizeBench I'm taking the approach of "constructor gets the object into a valid state" so property
        // setters ought not be required for an object to be considered fully composed/resolved.
        var propInjector = container.Kernel.ComponentModelBuilder
                                           .Contributors
                                           .OfType<PropertiesDependenciesModelInspector>()
                                           .Single();
        container.Kernel.ComponentModelBuilder.RemoveContributor(propInjector);

        // Allow resolving arrays - this is handy when there can be multiple implementations of something (like IBinaryLocator),
        // which a type wants to resolve into an array it can iterate over.
        container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel, true));

        container.Register(Component.For<IWindsorContainer>()
                                    .Instance(container)
                                    .LifestyleSingleton());

        container.Register(Classes.FromAssembly(typeof(MainWindow).Assembly)
                                  .IncludeNonPublicTypes()
                                  .BasedOn<Window>()
                                  .WithServiceBase()
                                  .WithServiceAllInterfaces()
                                  .LifestyleTransient()
                                  .Configure(window => window.Named(window.Implementation.Name))
                                  );

        container.Register(Component.For<ISessionFactory>()
                                    .Instance(new SessionFactory())
                                    .LifestyleSingleton());

        container.Register(Classes.FromAssembly(typeof(MainWindow).Assembly)
                                  .IncludeNonPublicTypes()
                                  .Where(c => c.Name.EndsWith("ViewModel", StringComparison.Ordinal) &&
                                              !c.Name.EndsWith("MainWindowViewModel", StringComparison.Ordinal))
                                  .WithServiceSelf()
                                  .WithServiceAllInterfaces()
                                  .LifestyleTransient());

        // MainWindowViewModel is a special view model since there'll only ever be one of them,
        // so make sure it's a singleton.
        container.Register(Classes.FromAssembly(typeof(MainWindow).Assembly)
                                  .IncludeNonPublicTypes()
                                  .Where(c => c.Name.EndsWith("MainWindowViewModel", StringComparison.Ordinal))
                                  .WithServiceSelf()
                                  .WithServiceAllInterfaces()
                                  .LifestyleSingleton());

        container.Register(Component.For<App>().LifestyleSingleton());
    }
}
