import { Badge } from "@mantine/core";

import { VERDICT_COLOR, type Verdict } from "./verdict";

/** A check's verdict as a chip, coloured the same on every page; `label` replaces the word (a count of failed checks). */
export function VerdictBadge({ verdict, label }: { verdict: Verdict; label?: string }) {
    return (
        <Badge color={VERDICT_COLOR[verdict]} variant={verdict === "failed" ? "filled" : "light"}>
            {label ?? verdict}
        </Badge>
    );
}
