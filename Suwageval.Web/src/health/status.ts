export type ServerStatus = { kind: "checking" } | { kind: "up" } | { kind: "down"; reason: string };

export function statusOf(failure: Error | null): ServerStatus {
    return failure === null ? { kind: "up" } : { kind: "down", reason: failure.message };
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
