import type { ChangedCheck, RecheckPlan, RunSummary } from "./api";

export const RECHECK_EXPLANATION =
    "Judges this run's stored answers again with the current checks, without calling the agent. Use it after changing or adding checks: unchanged checks keep their verdicts, changed and new ones are evaluated, and scenarios changed since the run are skipped.";

export function recheckTooltip(status: RunSummary["status"]): string {
    return recheckDisabledReason(status) ?? RECHECK_EXPLANATION;
}

export function recheckDisabledReason(status: RunSummary["status"]): string | null {
    return status === "Done" ? null : "Only a finished run can be re-checked.";
}

export interface ScenarioChanges {
    scenarioKey: string;
    checks: ChangedCheck[];
}

export function changesByScenario(checks: readonly ChangedCheck[]): ScenarioChanges[] {
    const groups: ScenarioChanges[] = [];
    for (const check of checks) {
        const group = groups.find((existing) => existing.scenarioKey === check.scenarioKey);
        if (group) group.checks.push(check);
        else groups.push({ scenarioKey: check.scenarioKey, checks: [check] });
    }
    return groups;
}

export function planHeadline(plan: Pick<RecheckPlan, "changedChecks">): string {
    const count = plan.changedChecks.length;
    return `${count} ${count === 1 ? "check is" : "checks are"} evaluated again; every other check keeps its verdict from the run.`;
}

export function judgedNote(plan: Pick<RecheckPlan, "judgedChecks">): string | null {
    const judged = plan.judgedChecks;
    return judged > 0 ? `${judged} judged by a model: ${judged === 1 ? "it calls" : "they call"} the judge's model.` : null;
}
