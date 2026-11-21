import { Badge } from "@maniumek/suwfrontkit";

import { VERDICT_VARIANT, type Verdict } from "./verdict";

export function VerdictBadge({ verdict, label }: { verdict: Verdict; label?: string }) {
    return <Badge variant={VERDICT_VARIANT[verdict]}>{label ?? verdict}</Badge>;
}
