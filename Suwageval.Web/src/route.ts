// The pages of the app and the paths that open them. No router library: the entry point reads
// the pathname once, and links are plain `<a href>` (a full page load, so the back button works).
const GUID = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
const RUN_PATH = new RegExp(`^/runs/(${GUID})/?$`, "i");
const AGENT_PATH = /^\/agents\/([^/]+)\/?$/;
const VERSION_PATH = new RegExp(`^/agents/([^/]+)/versions/(${GUID})/?$`, "i");

export type Page =
    | { kind: "agents" }
    | { kind: "agent"; name: string }
    | { kind: "version"; name: string; versionId: string }
    | { kind: "run"; runId: string }
    | { kind: "compare"; runs: string }
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
 * or `/compare?runs=<guid>,<guid>,…` (which `suwageval compare` opens; `runs` is passed on as it is, the page checks it).
 */
export function pageOf(pathname: string, search = ""): Page {
    if (pathname === "/") return { kind: "agents" };
    if (pathname === "/compare" || pathname === "/compare/")
        return { kind: "compare", runs: new URLSearchParams(search).get("runs") ?? "" };

    const run = RUN_PATH.exec(pathname);
    if (run?.[1]) return { kind: "run", runId: run[1] };

    const version = VERSION_PATH.exec(pathname);
    const versionAgent = version?.[1] === undefined ? null : decode(version[1]);
    if (version?.[2] && versionAgent !== null) return { kind: "version", name: versionAgent, versionId: version[2] };

    const agent = AGENT_PATH.exec(pathname);
    const agentName = agent?.[1] === undefined ? null : decode(agent[1]);
    if (agentName !== null) return { kind: "agent", name: agentName };

    return { kind: "unknown" };
}
