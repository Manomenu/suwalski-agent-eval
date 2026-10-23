// "Hide passed checks" on the run page: the switch in the address (`&hide=passed`) and what it keeps. A check passed when it reached
// its threshold and no sample of it went unjudged; a scenario passed when it has checks, all of them passed and its samples all answered.
// Failed checks, checks not judged and samples with an agent error always stay. The CLI's `results --failed-only` follows the same rules.
import type { CheckRunResult, ScenarioRunResult } from "./api";
import { notJudgedCount } from "./results";

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

/** A check that passed for good: its threshold was reached and every sample of it was judged. */
export function checkPassed(check: CheckRunResult): boolean {
    return check.passed && notJudgedCount(check) === 0;
}

/** A scenario with nothing to look at: it has checks, all passed, and the agent answered every sample. */
export function scenarioPassed(scenario: ScenarioRunResult): boolean {
    return scenario.checks.length > 0 && scenario.checks.every(checkPassed) && scenario.samples.every((sample) => sample.status === "Done");
}

/** The checks of a scenario to show: all, or without the passed ones. */
export function visibleChecks(scenario: ScenarioRunResult, hidePassed: boolean): CheckRunResult[] {
    return hidePassed ? scenario.checks.filter((check) => !checkPassed(check)) : scenario.checks;
}

/** The scenarios to show: all, or without those that passed. */
export function visibleScenarios(scenarios: readonly ScenarioRunResult[], hidePassed: boolean): ScenarioRunResult[] {
    return hidePassed ? scenarios.filter((scenario) => !scenarioPassed(scenario)) : [...scenarios];
}

/** How many passed checks (in the scenarios still shown or not) and passed scenarios the switch hides. */
export function hiddenCounts(scenarios: readonly ScenarioRunResult[]): { checks: number; scenarios: number } {
    return {
        checks: scenarios.reduce((total, scenario) => total + scenario.checks.filter(checkPassed).length, 0),
        scenarios: scenarios.filter(scenarioPassed).length,
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
