// The pages of the app and the paths that open them. No router library: the entry point reads
// the pathname once, and links are plain `<a href>` (a full page load, so the back button works).
const GUID = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
const RUN_PATH = new RegExp(`^/runs/(${GUID})/?$`, "i");
const AGENT_PATH = /^\/agents\/([^/]+)\/?$/;
const VERSION_COMPARE_PATH = /^\/agents\/([^/]+)\/versions\/compare\/?$/;
const GUID_ONLY = new RegExp(`^${GUID}$`, "i");
const VERSION_PATH = new RegExp(`^/agents/([^/]+)/versions/(${GUID})/?$`, "i");

export type Page =
    | { kind: "agents" }
    | { kind: "agent"; name: string }
    | { kind: "version"; name: string; versionId: string }
    | { kind: "versionCompare"; name: string; a: string; b: string }
    | { kind: "run"; runId: string }
    | { kind: "compare"; runs: string; group: string; agent: string; baseline: string }
    | { kind: "unknown" };

function decode(segment: string): string | null {
    try {
        return decodeURIComponent(segment);
    } catch {
        return null;
    }
}

/**
 * The page a path opens: `/`, `/agents/<name>`, `/agents/<name>/versions/<guid>`, `/runs/<guid>` (which `suwageval results` opens)
 * `/agents/<name>/versions/compare?a=<guid>&b=<guid>` (what changed between two versions; `suwageval versions diff` opens it),
 * or `/compare?runs=<guid>,<guid>,…` / `/compare?group=<name>[&agent=<name>]` (runs side by side; `suwageval compare` and `suwageval sweep`
 * open it), each with an optional `&baseline=<guid>` (one of the runs: the others are read against it). The queries are passed on as
 * they are, the page checks them.
 */
export function pageOf(pathname: string, search = ""): Page {
    if (pathname === "/") return { kind: "agents" };
    if (pathname === "/compare" || pathname === "/compare/") {
        const query = new URLSearchParams(search);
        return {
            kind: "compare",
            runs: query.get("runs") ?? "",
            group: query.get("group") ?? "",
            agent: query.get("agent") ?? "",
            baseline: query.get("baseline") ?? "",
        };
    }

    const run = RUN_PATH.exec(pathname);
    if (run?.[1]) return { kind: "run", runId: run[1] };

    const versionCompare = VERSION_COMPARE_PATH.exec(pathname);
    const compareAgent = versionCompare?.[1] === undefined ? null : decode(versionCompare[1]);
    if (compareAgent !== null) {
        const query = new URLSearchParams(search);
        const a = query.get("a") ?? "";
        const b = query.get("b") ?? "";
        return GUID_ONLY.test(a) && GUID_ONLY.test(b) ? { kind: "versionCompare", name: compareAgent, a, b } : { kind: "unknown" };
    }

    const version = VERSION_PATH.exec(pathname);
    const versionAgent = version?.[1] === undefined ? null : decode(version[1]);
    if (version?.[2] && versionAgent !== null) return { kind: "version", name: versionAgent, versionId: version[2] };

    const agent = AGENT_PATH.exec(pathname);
    const agentName = agent?.[1] === undefined ? null : decode(agent[1]);
    if (agentName !== null) return { kind: "agent", name: agentName };

    return { kind: "unknown" };
}
