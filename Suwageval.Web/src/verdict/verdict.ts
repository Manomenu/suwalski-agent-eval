// A check's verdict as every page shows it: a run's checks and a monitored conversation's.
import { itemKey, type ConversationItem } from "../conversation/items";

/** What a check said: passed, failed, or not judged (the model that gives its verdict did not answer). */
export type Verdict = "passed" | "failed" | "not judged";

/** One verdict as the server sends it, the parts the pages read. */
export interface CheckOutcome {
    passed: boolean;
    notJudged?: boolean | null;
    evidence?: readonly ConversationItem[] | null;
}

/** The colour of each verdict, the same on every page. */
export const VERDICT_COLOR: Record<Verdict, "green" | "red" | "yellow"> = { passed: "green", failed: "red", "not judged": "yellow" };

export function verdictOf(outcome: CheckOutcome): Verdict {
    if (outcome.passed) return "passed";
    return outcome.notJudged === true ? "not judged" : "failed";
}

/** The items the checks that did not pass point at, for the conversation to mark as evidence. */
export function failedEvidence(outcomes: readonly CheckOutcome[]): Set<string> {
    return new Set(outcomes.filter((outcome) => !outcome.passed).flatMap((outcome) => (outcome.evidence ?? []).map(itemKey)));
}
