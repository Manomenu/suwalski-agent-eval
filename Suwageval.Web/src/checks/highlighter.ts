// The YAML highlighter, built on the first use: shiki's fine-grained core with only the YAML grammar, one theme and the
// JavaScript regex engine (no WebAssembly). Every part is a dynamic import, so none of it is in the first bundle.
import type { HighlighterCore } from "shiki/core";

export const DEFINITION_THEME = "catppuccin-frappe";

let highlighter: Promise<HighlighterCore> | undefined;

/** The one highlighter; a failed load is forgotten, so the next opening tries again. */
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
