// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Not importat for test code")]

[assembly: SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Not important for tests - in fact TestClass types MUST be public for MSTest so doing this loses test coverage.")]