export function runPrintLine(run: { id: string; agent: string }, selectedTags: readonly string[], hidePassed: boolean): string {
    const parts = [
        `Run ${run.id} of ${run.agent}`,
        selectedTags.length === 0 ? "all scenarios" : `scenarios with tags: ${selectedTags.join(", ")}`,
    ];
    if (hidePassed) parts.push("passed checks hidden");
    return parts.join(" · ");
}
