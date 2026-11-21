import { Badge, Paper } from "@maniumek/suwfrontkit";

import { itemBody, itemKey, itemTitle, type ConversationItem } from "./items";

export function ConversationItems({
    items,
    highlight = NONE,
    simulatedUser = false,
}: {
    items: ConversationItem[];
    highlight?: ReadonlySet<string>;
    simulatedUser?: boolean;
}) {
    return (
        <div className="flex flex-col gap-2.5">
            {items.map((item, index) => {
                const marked = highlight.has(itemKey(item));
                const simulated = simulatedUser && item.kind === "UserMessage";
                return (
                    <Paper key={index} className={`p-2.5${marked ? " border-destructive" : ""}`}>
                        <Badge variant={marked ? "danger" : simulated ? "info" : "neutral"} className="mb-1">
                            {simulated ? "simulated user" : itemTitle(item)}
                            {marked ? " · evidence" : ""}
                        </Badge>
                        <pre className="overflow-x-auto rounded-md bg-secondary p-3 font-mono text-[13px]">{itemBody(item)}</pre>
                    </Paper>
                );
            })}
        </div>
    );
}

const NONE: ReadonlySet<string> = new Set();
