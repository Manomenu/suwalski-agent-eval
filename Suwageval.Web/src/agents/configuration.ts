/** The regression history when agent.yaml does not set one (the server's `RegressionSettings.DefaultHistory`). */
export const DEFAULT_REGRESSION_HISTORY = 5;

const DEFAULT_BRANCH_TEXT = "the repository's default branch";

/** The regression branches as shown: the listed ones, or the default branch when none (`isDefault`: shown dimmed). */
export function branchesText(branches: readonly string[] | null | undefined): { text: string; isDefault: boolean } {
    if (branches == null || branches.length === 0) return { text: DEFAULT_BRANCH_TEXT, isDefault: true };
    return { text: branches.join(", "), isDefault: false };
}

/** The regression history as shown: "N reference runs" (`isDefault`: not set, so the default; shown with a dimmed "(default)"). */
export function historyText(history: number | null | undefined): { text: string; isDefault: boolean } {
    const isDefault = history == null;
    const count = history ?? DEFAULT_REGRESSION_HISTORY;
    return { text: `${count} reference ${count === 1 ? "run" : "runs"}`, isDefault };
}
