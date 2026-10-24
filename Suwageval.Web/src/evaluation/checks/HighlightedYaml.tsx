import { Box, Code, Text } from "@mantine/core";
import { useEffect, useState } from "react";

import { hangingIndents } from "../../diff/hangingIndent";
import { DEFINITION_THEME, loadHighlighter } from "./highlighter";

const PRE_STYLE =
    "margin:0;white-space:pre-wrap;overflow-wrap:anywhere;font-family:var(--mantine-font-family-monospace);padding:var(--mantine-spacing-sm);border-radius:var(--mantine-radius-sm);font-size:var(--mantine-font-size-sm)";

/**
 * YAML in the theme's own colours, never scrolled sideways: a long line wraps and hangs under the start of its value
 * (`hangingIndents`), one block per line, as YAML would indent it. While the highlighter loads the plain text shows in a code block. The HTML comes from
 * shiki, which escapes the text it is given; it is set with `dangerouslySetInnerHTML` because turning shiki's tree into
 * React elements would need another package for no gain.
 */
export function HighlightedYaml({ yaml }: { yaml: string }) {
    const [highlighted, setHighlighted] = useState<{ yaml: string; html: string } | null>(null);
    const [failed, setFailed] = useState(false);

    useEffect(() => {
        let ignore = false;
        loadHighlighter().then(
            (highlighter) => {
                if (ignore) return;
                const hangs = hangingIndents(yaml.split("\n"));
                setHighlighted({
                    yaml,
                    html: highlighter.codeToHtml(yaml, {
                        lang: "yaml",
                        theme: DEFINITION_THEME,
                        transformers: [
                            {
                                pre(node) {
                                    node.properties.style = `${String(node.properties.style)};${PRE_STYLE}`;
                                },
                                code(node) {
                                    // The lines become blocks; the newlines between them would add empty lines.
                                    node.children = node.children.filter((child) => child.type !== "text");
                                },
                                line(node, line) {
                                    const hang = hangs[line - 1] ?? 0;
                                    node.properties.style = `display:block;padding-left:${String(hang)}ch;text-indent:-${String(hang)}ch`;
                                },
                            },
                        ],
                    }),
                });
            },
            () => {
                if (!ignore) setFailed(true);
            },
        );
        return () => {
            ignore = true;
        };
    }, [yaml]);

    if (highlighted?.yaml !== yaml) {
        return (
            <>
                <Code block style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>
                    {yaml}
                </Code>
                {failed && (
                    <Text size="xs" c="dimmed" mt="xs">
                        Syntax highlighting could not be loaded.
                    </Text>
                )}
            </>
        );
    }
    return <Box dangerouslySetInnerHTML={{ __html: highlighted.html }} />;
}
