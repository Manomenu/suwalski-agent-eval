// The rules of deleting a version or an agent, as plain functions: when the typed text confirms, what goes.

/** The deletion is confirmed by typing what is deleted (the version's id, the agent's name), exactly. */
export function confirmsDeletion(typed: string, expected: string): boolean {
    return typed === expected;
}

const count = (n: number, noun: string) => `${String(n)} ${noun}${n === 1 ? "" : "s"}`;

/** "2 versions, 5 scenarios, 7 checks, 3 runs, 12 samples and 36 verdicts will be deleted" — what goes with the agent. */
export function agentDeletionSummary(preview: {
    versions: number;
    scenarios: number;
    checks: number;
    runs: number;
    samples: number;
    verdicts: number;
}): string {
    return `${count(preview.versions, "version")}, ${count(preview.scenarios, "scenario")}, ${count(preview.checks, "check")}, ${count(preview.runs, "run")}, ${count(preview.samples, "sample")} and ${count(preview.verdicts, "verdict")} will be deleted`;
}

/** "3 runs, 12 samples and 36 verdicts will be deleted" — what the preview says goes with the version. */
export function deletionSummary(preview: { runs: number; samples: number; verdicts: number }): string {
    return `${count(preview.runs, "run")}, ${count(preview.samples, "sample")} and ${count(preview.verdicts, "verdict")} will be deleted`;
}
