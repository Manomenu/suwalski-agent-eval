namespace Suwageval.AgentSdk;

/// <summary>
/// A list <see cref="ContractJson.Options"/> leaves out of the JSON while it is empty: a field added to the contract later, so an
/// unchanged contract keeps the hash of its version and a reader that predates the field still reads it.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class OmittedWhenEmptyAttribute : Attribute;
