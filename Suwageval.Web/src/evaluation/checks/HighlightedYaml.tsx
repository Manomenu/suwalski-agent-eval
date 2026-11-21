import { useEffect, useState } from "react";

import { hangingIndents } from "./hangingIndent";
import { DEFINITION_THEME, loadHighlighter } from "./highlighter";

const PRE_STYLE = "margin:0;white-space:pre-wrap;overflow-wrap:anywhere";
const PRE_CLASS = "rounded-md p-3 font-mono text-sm";

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
                                    node.properties["class"] = `${String(node.properties["class"])} ${PRE_CLASS}`;
                                },
                                code(node) {
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
                <pre className="overflow-x-auto rounded-md bg-secondary p-3 font-mono text-[13px] whitespace-pre-wrap wrap-anywhere">
                    {yaml}
                </pre>
                {failed && <p className="mt-2.5 text-xs text-muted-foreground">Syntax highlighting could not be loaded.</p>}
            </>
        );
    }
    return <div dangerouslySetInnerHTML={{ __html: highlighted.html }} />;
}
