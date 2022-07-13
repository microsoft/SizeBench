// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
                           Justification = "SizeBench doesn't care about localization currently",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.SKUCrawler")]

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles",
                           Justification = "I have different naming preferences than FxCop",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.SKUCrawler")]

[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
                           Justification = "ConfigureAwait default is correct for app code, see this blog post by Stephen Toub: https://devblogs.microsoft.com/dotnet/configureawait-faq/")]

[assembly: SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
                           Justification = "None of SKUCrawler's SQL deals with user input",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.SKUCrawler")]
