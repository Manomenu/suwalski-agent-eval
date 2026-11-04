import type { HighlighterCore } from "shiki/core";

export const DEFINITION_THEME = "catppuccin-frappe";

let highlighter: Promise<HighlighterCore> | undefined;

export function loadHighlighter(): Promise<HighlighterCore> {
    highlighter ??= (async () => {
        const [{ createHighlighterCore }, { createJavaScriptRegexEngine }, yaml, theme] = await Promise.all([
            import("shiki/core"),
            import("shiki/engine/javascript"),
            import("shiki/langs/yaml.mjs"),
            import("shiki/themes/catppuccin-frappe.mjs"),
        ]);
        return createHighlighterCore({ langs: [yaml.default], themes: [theme.default], engine: createJavaScriptRegexEngine() });
    })().catch((error: unknown) => {
        highlighter = undefined;
        throw error;
    });
    return highlighter;
}
