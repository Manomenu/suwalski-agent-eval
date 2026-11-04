import type { CheckRunResult, ScenarioRunResult } from "./api";

const HIDE_PARAM = "hide";
const HIDE_PASSED = "passed";

export function hidePassedOf(search: string): boolean {
    return new URLSearchParams(search).getAll(HIDE_PARAM).includes(HIDE_PASSED);
}

export function searchWithHidePassed(search: string, hide: boolean): string {
    const query = new URLSearchParams(search);
    query.delete(HIDE_PARAM);
    if (hide) query.append(HIDE_PARAM, HIDE_PASSED);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}

export function visibleChecks(scenario: ScenarioRunResult, hidePassed: boolean): CheckRunResult[] {
    return hidePassed ? scenario.checks.filter((check) => !check.passedFully) : scenario.checks;
}

export function visibleScenarios(scenarios: readonly ScenarioRunResult[], hidePassed: boolean): ScenarioRunResult[] {
    return hidePassed ? scenarios.filter((scenario) => !scenario.passedFully) : [...scenarios];
}

export function hiddenCounts(scenarios: readonly ScenarioRunResult[]): { checks: number; scenarios: number } {
    return {
        checks: scenarios.reduce((total, scenario) => total + scenario.checks.filter((check) => check.passedFully).length, 0),
        scenarios: scenarios.filter((scenario) => scenario.passedFully).length,
    };
}

export function hiddenLine(counts: { checks: number; scenarios: number }): string {
    const checks = `${String(counts.checks)} passed ${counts.checks === 1 ? "check" : "checks"}`;
    const whole =
        counts.scenarios === 0
            ? ""
            : ` (${String(counts.scenarios)} ${counts.scenarios === 1 ? "scenario" : "scenarios"} passed entirely and ${counts.scenarios === 1 ? "is" : "are"} hidden whole)`;
    return `Hidden: ${checks}${whole}. Samples and Checks at the top still count the whole run.`;
}
