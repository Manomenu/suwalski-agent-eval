// Numbers that shape the app shell (same arrangement as the owner's other app: header, navbar, content).
export const HEADER_HEIGHT = 60;
export const NAVBAR_WIDTH = 220;
export const CONTENT_MAX_WIDTH = 1400;

/**
 * How wide a page's content may grow: CONTENT_MAX_WIDTH, but none for the pages that set their own widths — the agent's (its
 * analytics reach the right edge on a wide screen, app.css) and the comparison (up to 7 run columns: it takes the whole screen
 * and its table scrolls sideways beyond that).
 */
export function contentMaxWidth(kind: string): number | undefined {
    return kind === "agentEvaluation" || kind === "agentMonitoring" || kind === "compare" ? undefined : CONTENT_MAX_WIDTH;
}
/** The app icon beside its name in the header. */
export const LOGO_SIZE = 28;
