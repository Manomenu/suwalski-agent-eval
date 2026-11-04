export const HEADER_HEIGHT = 60;
export const NAVBAR_WIDTH = 220;
export const CONTENT_MAX_WIDTH = 1400;

export function contentMaxWidth(kind: string): number | undefined {
    return kind === "agentEvaluation" || kind === "agentMonitoring" || kind === "compare" ? undefined : CONTENT_MAX_WIDTH;
}
export const LOGO_SIZE = 28;
