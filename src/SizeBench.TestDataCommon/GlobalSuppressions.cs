
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Test code; don't care about coding style that much here")]

[assembly: SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "Not important for test code.")]

[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Not important for test code.")]

[assembly: SuppressMessage("Performance", "CA1854:Prefer the 'IDictionary.TryGetValue(TKey, out TValue)' method", Justification = "Performance of tests isn't *that* important.")]
