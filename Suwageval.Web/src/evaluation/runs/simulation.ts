import type { ConversationItem, ScenarioRunResult } from "./api";
import { formatSeconds } from "../../format/numbers";

type SampleResult = ScenarioRunResult["samples"][number];

export function isSimulated(sample: SampleResult): boolean {
    return sample.turnDurationsMs != null;
}

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

export function sampleTitle(sample: SampleResult): string {
    const parts = [isSimulated(sample) ? `Conversation, sample ${String(sample.sample)}` : `Agent output, sample ${String(sample.sample)}`];
    const turns = sample.turnDurationsMs?.length;
    if (turns != null) parts.push(turns === 1 ? "1 turn" : `${String(turns)} turns`);
    if (sample.endedByUser === true) parts.push("ended by the user");
    if (sample.durationMs != null) parts.push(formatSeconds(sample.durationMs));
    return parts.join(" · ");
}
