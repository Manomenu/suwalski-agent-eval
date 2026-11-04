import { Badge } from "@mantine/core";

import { useRequest } from "../api/useRequest";
import { fetchHealth } from "./api";
import { describe, statusOf, type ServerStatus as Status } from "./status";

export function ServerStatus() {
    const health = useRequest("health", fetchHealth);
    const status: Status =
        health.kind === "loading" ? { kind: "checking" } : statusOf(health.kind === "error" ? health.error : health.value);

    return (
        <Badge variant="light" size="lg" color={status.kind === "down" ? "red" : "mauve"}>
            server: {describe(status)}
        </Badge>
    );
}
