// The one route of the app: `/runs/<guid>`, which `suwageval results <runId>` opens.
const RUN_PATH = /^\/runs\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\/?$/i;

/** The run id a path points at, or null when the path is not a run page. */
export function runIdOf(pathname: string): string | null {
    return RUN_PATH.exec(pathname)?.[1] ?? null;
}
