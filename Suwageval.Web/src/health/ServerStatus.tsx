import { StatusBadge } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";

import { checkHealth } from "./api";
import { describe, statusOf, type ServerStatus as Status } from "./status";

const BADGE_OF_STATUS = { checking: "pending", up: "success", down: "danger" } as const;

export function ServerStatus() {
    const health = useQuery({ queryKey: ["health"], retry: false, queryFn: ({ signal }) => checkHealth(signal) });
    const status: Status = health.isPending ? { kind: "checking" } : statusOf(health.error);

    return <StatusBadge status={BADGE_OF_STATUS[status.kind]}>server: {describe(status)}</StatusBadge>;
}
