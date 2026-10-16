// Rules live in plain modules with a test beside them; components only wire them to React.
import type { Health } from "./api";

export type ServerStatus = { kind: "checking" } | { kind: "up" } | { kind: "down"; reason: string };

export function statusOf(result: Health | Error): ServerStatus {
    if (result instanceof Error) return { kind: "down", reason: result.message };
    return result.status === "ok" ? { kind: "up" } : { kind: "down", reason: result.status };
}

export function describe(status: ServerStatus): string {
    switch (status.kind) {
        case "checking":
            return "checking…";
        case "up":
            return "up";
        case "down":
            return `down (${status.reason})`;
    }
}
