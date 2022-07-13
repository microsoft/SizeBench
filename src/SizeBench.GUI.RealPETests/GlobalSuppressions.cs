
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Don't care about names in tests")]

[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
                           Justification = "ConfigureAwait default is correct for app code, and thus seems good for test code too, see this blog post by Stephen Toub: https://devblogs.microsoft.com/dotnet/configureawait-faq/")]

[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Class naming for tests isn't terribly important.")]
