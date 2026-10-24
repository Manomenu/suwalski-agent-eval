import { Badge, Code, Paper, Stack } from "@mantine/core";

import { itemBody, itemKey, itemTitle, type ConversationItem } from "./items";

/**
 * Items of a conversation, one under another: each with what it is and its text. `highlight` marks the ones a failed check points at;
 * with `simulatedUser`, the user's messages are marked as the simulated user's (not the agent's, and no check judges them).
 */
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
        <Stack gap="xs">
            {items.map((item, index) => {
                const marked = highlight.has(itemKey(item));
                const simulated = simulatedUser && item.kind === "UserMessage";
                return (
                    // Items have no id and may repeat, so the position is their identity.
                    <Paper key={index} withBorder p="xs" style={marked ? { borderColor: "var(--mantine-color-red-6)" } : undefined}>
                        <Badge variant="light" color={marked ? "red" : simulated ? "blue" : "gray"} mb={4}>
                            {simulated ? "simulated user" : itemTitle(item)}
                            {marked ? " · evidence" : ""}
                        </Badge>
                        <Code block>{itemBody(item)}</Code>
                    </Paper>
                );
            })}
        </Stack>
    );
}

const NONE: ReadonlySet<string> = new Set();
