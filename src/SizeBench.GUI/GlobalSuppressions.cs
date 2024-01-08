// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
                           Justification = "SizeBench doesn't care about localization currently")]

[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
                            Justification = "SizeBench never uses ToLowerInvariant for security purposes, only for logging, or things like displaying C++ keywords, where lowercase is easier on the eyes and more correct",
                            Scope = "namespaceanddescendants", Target = "~N:SizeBench.GUI.Pages")]

[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
                           Justification = "ConfigureAwait default is correct for app code, see this blog post by Stephen Toub: https://devblogs.microsoft.com/dotnet/configureawait-faq/")]

[assembly: SuppressMessage("Design", "CA1030:Use events where appropriate",
                           Justification = "This code analysis rule is based on naming patterns (like starting with Fire* in a function name), and my naming style disagrees with FxCop.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.GUI")]
[assembly: SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
                           Justification = "This code analysis rule is based on naming patterns, and I prefer a different style for P/Invokes.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.GUI.Windows")]

[assembly: SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression",
                           Justification = "The suppressions that can be removed get too noisy with VS and SDK updates changing the rules over time, and they're harmless to leave in.  Ignoring this to avoid noise in the VS Error List window.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.GUI")]

[assembly: SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", 
                           Justification = "Argument exceptions ordering isn't that important in the GUI, it's not a library used externally.  Also, there have been some regressions in CA2208 in the .NET 8 SDK where it has false positives (see https://github.com/dotnet/roslyn-analyzers/issues/6863)",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.GUI")]
