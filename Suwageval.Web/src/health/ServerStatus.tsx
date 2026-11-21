import { Badge } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";

import { checkHealth } from "./api";
import { describe, statusOf, type ServerStatus as Status } from "./status";

const BADGE_OF_STATUS = {
    checking: { variant: "info", dot: "pulsing" },
    up: { variant: "success", dot: true },
    down: { variant: "danger", dot: true },
} as const;

export function ServerStatus() {
    const health = useQuery({ queryKey: ["health"], retry: false, queryFn: ({ signal }) => checkHealth(signal) });
    const status: Status = health.isPending ? { kind: "checking" } : statusOf(health.error);
    const { variant, dot } = BADGE_OF_STATUS[status.kind];

    return (
        <Badge variant={variant} dot={dot}>
            server: {describe(status)}
        </Badge>
    );
}
