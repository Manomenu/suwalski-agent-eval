// "Hide passed checks" on the run page: the switch in the address (`&hide=passed`) and what it keeps. What "passed" means is the
// server's (`passedFully` on each check and scenario of the results, decided once in RunReport); the CLI's `results --failed-only`
// reads the same fields, so the two never differ.
import type { CheckRunResult, ScenarioRunResult } from "./api";

const HIDE_PARAM = "hide";
const HIDE_PASSED = "passed";

/** Whether the address asks to hide the passed checks (`?hide=passed`). */
export function hidePassedOf(search: string): boolean {
    return new URLSearchParams(search).getAll(HIDE_PARAM).includes(HIDE_PASSED);
}

/** The query with the hide parameter set (or removed); every other parameter stays. */
export function searchWithHidePassed(search: string, hide: boolean): string {
    const query = new URLSearchParams(search);
    query.delete(HIDE_PARAM);
    if (hide) query.append(HIDE_PARAM, HIDE_PASSED);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}

/** The checks of a scenario to show: all, or without the passed ones. */
export function visibleChecks(scenario: ScenarioRunResult, hidePassed: boolean): CheckRunResult[] {
    return hidePassed ? scenario.checks.filter((check) => !check.passedFully) : scenario.checks;
}

/** The scenarios to show: all, or without those that passed. */
export function visibleScenarios(scenarios: readonly ScenarioRunResult[], hidePassed: boolean): ScenarioRunResult[] {
    return hidePassed ? scenarios.filter((scenario) => !scenario.passedFully) : [...scenarios];
}

/** How many passed checks (in the scenarios still shown or not) and passed scenarios the switch hides. */
export function hiddenCounts(scenarios: readonly ScenarioRunResult[]): { checks: number; scenarios: number } {
    return {
        checks: scenarios.reduce((total, scenario) => total + scenario.checks.filter((check) => check.passedFully).length, 0),
        scenarios: scenarios.filter((scenario) => scenario.passedFully).length,
    };
}

/**
 * The line under the switch: how many passed checks are hidden, how many scenarios went whole (their checks are among those),
 * and that the Samples and Checks counts at the top of the page still count the whole run.
 */
export function hiddenLine(counts: { checks: number; scenarios: number }): string {
    const checks = `${String(counts.checks)} passed ${counts.checks === 1 ? "check" : "checks"}`;
    const whole =
        counts.scenarios === 0
            ? ""
            : ` (${String(counts.scenarios)} ${counts.scenarios === 1 ? "scenario" : "scenarios"} passed entirely and ${counts.scenarios === 1 ? "is" : "are"} hidden whole)`;
    return `Hidden: ${checks}${whole}. Samples and Checks at the top still count the whole run.`;
}
