import type { ConversationItem, ScenarioRunResult } from "./api";
import { formatSeconds } from "./results";

type SampleResult = ScenarioRunResult["samples"][number];

/** A sample of a simulated scenario: only those carry the time of each turn. */
export function isSimulated(sample: SampleResult): boolean {
    return sample.turnDurationsMs != null;
}

/**
 * A simulation's output turn by turn, as the server splits it: every user message (the simulated user's — the agent never produces one)
 * opens a turn, and what comes before the first one is the turn the agent started.
 */
export function simulationTurns(items: ConversationItem[]): ConversationItem[][] {
    const turns: ConversationItem[][] = [];
    let turn: ConversationItem[] = [];
    for (const item of items) {
        if (item.kind === "UserMessage" && turn.length > 0) {
            turns.push(turn);
            turn = [];
        }
        turn.push(item);
    }
    if (turn.length > 0) turns.push(turn);
    return turns;
}

/** The title of a sample's output: "Agent output, sample 2 · 1.2 s", or for a simulation "Conversation, sample 2 · 3 turns · ended by the user · 4.0 s". */
export function sampleTitle(sample: SampleResult): string {
    const parts = [isSimulated(sample) ? `Conversation, sample ${String(sample.sample)}` : `Agent output, sample ${String(sample.sample)}`];
    const turns = sample.turnDurationsMs?.length;
    if (turns != null) parts.push(turns === 1 ? "1 turn" : `${String(turns)} turns`);
    if (sample.endedByUser === true) parts.push("ended by the user");
    if (sample.durationMs != null) parts.push(formatSeconds(sample.durationMs));
    return parts.join(" · ");
}
