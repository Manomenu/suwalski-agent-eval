// What the re-check button and its dialog show, as plain functions.
import type { ChangedCheck, RecheckPlan, RunSummary } from "./api";

/** What the Re-check button does, said on hover. */
export const RECHECK_EXPLANATION =
    "Judges this run's stored answers again with the current checks, without calling the agent. Use it after changing or adding checks: unchanged checks keep their verdicts, changed and new ones are evaluated, and scenarios changed since the run are skipped.";

/** The button's tooltip: why it is disabled, else what it does. */
export function recheckTooltip(status: RunSummary["status"]): string {
    return recheckDisabledReason(status) ?? RECHECK_EXPLANATION;
}

/** Only a finished run has all its answers: the button is offered disabled for any other. */
export function recheckDisabledReason(status: RunSummary["status"]): string | null {
    return status === "Done" ? null : "Only a finished run can be re-checked.";
}

export interface ScenarioChanges {
    scenarioKey: string;
    checks: ChangedCheck[];
}

/** The changed checks grouped by scenario, in the order the plan has them. */
export function changesByScenario(checks: readonly ChangedCheck[]): ScenarioChanges[] {
    const groups: ScenarioChanges[] = [];
    for (const check of checks) {
        const group = groups.find((existing) => existing.scenarioKey === check.scenarioKey);
        if (group) group.checks.push(check);
        else groups.push({ scenarioKey: check.scenarioKey, checks: [check] });
    }
    return groups;
}

/** "2 checks are evaluated again" for a plan that can start. */
export function planHeadline(plan: Pick<RecheckPlan, "changedChecks">): string {
    const count = plan.changedChecks.length;
    return `${count} ${count === 1 ? "check is" : "checks are"} evaluated again; every other check keeps its verdict from the run.`;
}

/** Said when some of the checks to evaluate call the judge's model; null when none does. */
export function judgedNote(plan: Pick<RecheckPlan, "judgedChecks">): string | null {
    const judged = plan.judgedChecks;
    return judged > 0 ? `${judged} judged by a model: ${judged === 1 ? "it calls" : "they call"} the judge's model.` : null;
}
