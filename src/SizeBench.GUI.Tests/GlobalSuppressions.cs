
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Don't care about names in tests", Scope = "module")]

[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
                           Justification = "ConfigureAwait default is correct for app code, and thus seems good for test code too, see this blog post by Stephen Toub: https://devblogs.microsoft.com/dotnet/configureawait-faq/")]

[assembly: SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "This isn't important for test code.")]

[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Not important for test code")]

[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Not important for test code")]
