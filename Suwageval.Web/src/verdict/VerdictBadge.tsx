import { Badge } from "@mantine/core";

import { VERDICT_COLOR, type Verdict } from "./verdict";

export function VerdictBadge({ verdict, label }: { verdict: Verdict; label?: string }) {
    return (
        <Badge color={VERDICT_COLOR[verdict]} variant={verdict === "failed" ? "filled" : "light"}>
            {label ?? verdict}
        </Badge>
    );
}
