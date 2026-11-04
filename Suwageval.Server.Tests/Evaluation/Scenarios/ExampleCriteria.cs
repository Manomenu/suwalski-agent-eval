namespace Suwageval.Server.Tests.Evaluation.Scenarios;

// Why: these ids are data of an agent under test, from that agent's spec, not criteria of this repository; criteria.json lists this file as not a guardrail
internal static class ExampleCriteria
{
    public const string Refunds1 = "AC-refunds-1";
    public const string Refunds2 = "AC-refunds-2";
    public const string Refunds3 = "AC-refunds-3";
    public const string TwoWordFeature = "AC-refund-limits-12";
    public const string DigitInFeature = "AC-x2-30";
    public const string NumberZero = "AC-refunds-0";
    public const string LeadingZero = "AC-refunds-01";
}
