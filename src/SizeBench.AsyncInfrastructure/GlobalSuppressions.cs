using System.Diagnostics.CodeAnalysis;

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
                            Justification = "SizeBench does not support localization, so don't care about this rule currently.",
                            Scope = "namespaceanddescendants", Target = "~N:SizeBench.AsyncInfrastructure")]
