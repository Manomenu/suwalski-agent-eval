import { itemKey, type ConversationItem } from "../conversation/items";

export type Verdict = "passed" | "failed" | "not judged";

export interface CheckOutcome {
    passed: boolean;
    notJudged?: boolean | null;
    evidence?: readonly ConversationItem[] | null;
}

export const VERDICT_COLOR: Record<Verdict, "green" | "red" | "yellow"> = { passed: "green", failed: "red", "not judged": "yellow" };

export function verdictOf(outcome: CheckOutcome): Verdict {
    if (outcome.passed) return "passed";
    return outcome.notJudged === true ? "not judged" : "failed";
}

export function failedEvidence(outcomes: readonly CheckOutcome[]): Set<string> {
    return new Set(outcomes.filter((outcome) => !outcome.passed).flatMap((outcome) => (outcome.evidence ?? []).map(itemKey)));
}
