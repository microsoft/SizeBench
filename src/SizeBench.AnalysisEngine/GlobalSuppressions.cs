// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
                           Justification = "SizeBench doesn't care about localization currently",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]

[assembly: SuppressMessage("Design", "CA1030:Use events where appropriate",
                           Justification = "This code analysis rule is based on naming patterns (like starting with Fire* in a function name), and my naming style disagrees with FxCop.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]

[assembly: SuppressMessage("Design", "CA1032:Implement standard exception constructors",
                           Justification = "Exceptions in SizeBench are never serialized, so these extra constructors aren't useful.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]

[assembly: SuppressMessage("Design", "CA1060:Move pinvokes to native methods class",
                           Justification = "This code analysis rule is based on naming patterns, and my naming style is different than what FxCop likes.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]

[assembly: SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix",
                           Justification = "This code analysis rule is based on naming patterns, and my naming style is different than what FxCop likes.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]

[assembly: SuppressMessage("Design", "CA1068:CancellationToken parameters must come last",
                           Justification = "SizeBench has many things that sort of make sense to be the last parameter - a 'parentLogger', a progress reporter, a CancellationToken, and none of them is obviously better than the others so this naming rule is too restrictive.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]

[assembly: SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler",
                           Justification = "I use a custom TaskFactory and this warning seems extremely noisy/pointless in this case.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]

[assembly: SuppressMessage("Style", "IDE0042:Deconstruct variable declaration",
                           Justification = "I don't want this warning on for the whole project, deconstruction has a perf cost and shouldn't be blindly used for style.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]

[assembly: SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression",
                           Justification = "The suppressions that can be removed get too noisy with VS and SDK updates changing the rules over time, and they're harmless to leave in.  Ignoring this to avoid noise in the VS Error List window.",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.AnalysisEngine")]
