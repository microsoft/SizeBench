// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;
[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
                           Justification = "SizeBench never uses ToLowerInvariant for security purposes, only for logging, where lowercase is easier on the eyes",
                           Scope = "namespaceanddescendants", Target = "~N:SizeBench.ErrorReporting")]
