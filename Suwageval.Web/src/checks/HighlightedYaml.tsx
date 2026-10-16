import { Box, Code, Text } from "@mantine/core";
import { useEffect, useState } from "react";

import { DEFINITION_THEME, loadHighlighter } from "./highlighter";

const PRE_STYLE =
    "margin:0;overflow-x:auto;padding:var(--mantine-spacing-sm);border-radius:var(--mantine-radius-sm);font-size:var(--mantine-font-size-sm)";

/**
 * YAML in the theme's own colours. While the highlighter loads the plain text shows in a code block. The HTML comes from
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
                <Code block>{yaml}</Code>
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
