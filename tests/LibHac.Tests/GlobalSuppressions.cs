// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Assertions", "xUnit2007:Do not use typeof expression to check the type", Justification = "Analyzer doesn't apply to Xunit internals.", Scope = "namespaceanddescendants", Target = "Xunit")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Assertions", "xUnit2015:Do not use typeof expression to check the exception type", Justification = "Analyzer doesn't apply to Xunit internals.", Scope = "namespaceanddescendants", Target = "Xunit")]