using System.Runtime.CompilerServices;

// Lets OctopusSample.Tests.Editor exercise the sample's internal startup/restore surface
// (OctopusScenarioSdk, OctopusSampleHomeView) directly, without reflection.
[assembly: InternalsVisibleTo("OctopusSample.Tests.Editor")]
