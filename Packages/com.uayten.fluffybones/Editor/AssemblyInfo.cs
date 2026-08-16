using System.Runtime.CompilerServices;

// The inspector's fix-up for a list entry Unity created by zeroing it is worth a test
// of its own — a chain born frozen is silent, and the person who hits it has no way to
// guess why the bones will not move. The method stays out of the public API: opening it
// to the test assembly is narrower than making it something anyone can call.
[assembly: InternalsVisibleTo("FluffyBones.Tests.Editor")]
