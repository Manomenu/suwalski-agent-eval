// What the versions pages show, as plain functions and texts: labels, which versions are compared, the JSON of a schema.

/** A version's label, or a dash for one registered without it. */
export function versionLabel(version: { label?: string | null }): string {
    return version.label?.trim() ? version.label : "—";
}

/** The two selected versions to compare, or null unless exactly two are selected. */
export function versionsToCompare(versions: { id: string }[], selected: ReadonlySet<string>): [string, string] | null {
    const chosen = versions.filter((version) => selected.has(version.id));
    const [first, second] = chosen;
    return first && second && chosen.length === 2 ? [first.id, second.id] : null;
}

/** The tooltip of "Compare versions": what it does, or why it cannot yet. */
export function compareVersionsTitle(ready: boolean): string {
    return ready ? "Show what changed between the two selected versions" : "Select exactly two versions to compare them";
}

/** A JSON value (a schema, model parameters) as indented text for a code block. */
export function jsonText(value: unknown): string {
    return JSON.stringify(value, null, 2);
}

/** What an agent version is, said on hover where versions are listed (the runs table, the Versions section). */
export const VERSION_HELP =
    "The agent version the run used: its contract as the agent served it when the run started (LLM agents, prompts, models, tools, events, data version). Any change there is a new version; open it to see the contract, or tick two in Versions to compare them.";

/** What the Versions section's heading and columns say on hover. */
export const VERSIONS_SECTION_HELP =
    "A version is the content of the agent's contract (prompts, models, tools, settings); any change makes a new version.";
export const VERSION_LABEL_HELP =
    "The name the contract gave this version (its label), if any; the version keeps the first one it was seen with.";
export const VERSION_CREATED_HELP = "When a run first met this contract, which is when the version was made.";
export const VERSION_RUNS_HELP = "How many runs of this version are in no group: the runs the Evaluation page lists.";
export const VERSION_GROUP_RUNS_HELP =
    "How many runs of this version are in a group (run --group); a re-check counts in its original's group.";
export const VERSION_CONVERSATIONS_HELP =
    "How many conversations of the deployed agent this version answered the last turn of in the last 30 days (in the chosen environment, when one is).";
export const VERSION_LAST_CONVERSATION_HELP =
    "When this version last answered a turn of the deployed agent. Current: it is the version answering there now. Not evaluated: it answers conversations and has no run.";
export const VERSION_ID_HELP = "The version's id; open it to see the contract.";
