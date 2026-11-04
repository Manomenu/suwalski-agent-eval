export function confirmsDeletion(typed: string, expected: string): boolean {
    return typed === expected;
}

const count = (n: number, noun: string) => `${String(n)} ${noun}${n === 1 ? "" : "s"}`;

function summary(parts: string[], conversations: number | undefined): string {
    const all = conversations ? [...parts, count(conversations, "monitored conversation")] : parts;
    return `${all.slice(0, -1).join(", ")} and ${all[all.length - 1] ?? ""} will be deleted`;
}

export function agentDeletionSummary(preview: {
    versions: number;
    scenarios: number;
    checks: number;
    runs: number;
    samples: number;
    verdicts: number;
    conversations?: number;
}): string {
    return summary(
        [
            count(preview.versions, "version"),
            count(preview.scenarios, "scenario"),
            count(preview.checks, "check"),
            count(preview.runs, "run"),
            count(preview.samples, "sample"),
            count(preview.verdicts, "verdict"),
        ],
        preview.conversations,
    );
}

export function deletionSummary(preview: { runs: number; samples: number; verdicts: number; conversations?: number }): string {
    return summary(
        [count(preview.runs, "run"), count(preview.samples, "sample"), count(preview.verdicts, "verdict")],
        preview.conversations,
    );
}
