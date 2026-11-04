export const DEFAULT_REGRESSION_HISTORY = 5;

export const DEFAULT_SAMPLES = 3;

const DEFAULT_BRANCH_TEXT = "the repository's default branch";

export function branchesText(branches: readonly string[] | null | undefined): { text: string; isDefault: boolean } {
    if (branches == null || branches.length === 0) return { text: DEFAULT_BRANCH_TEXT, isDefault: true };
    return { text: branches.join(", "), isDefault: false };
}

export function historyText(history: number | null | undefined): { text: string; isDefault: boolean } {
    const isDefault = history == null;
    const count = history ?? DEFAULT_REGRESSION_HISTORY;
    return { text: `${count} reference ${count === 1 ? "run" : "runs"}`, isDefault };
}

export function samplesText(samples: number | null | undefined): { text: string; isDefault: boolean } {
    return { text: String(samples ?? DEFAULT_SAMPLES), isDefault: samples == null };
}
