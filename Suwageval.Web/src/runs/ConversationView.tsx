import { Badge, Code, Paper, Stack, Text } from "@mantine/core";

import type { ConversationItem } from "./api";
import { itemBody, itemKey, itemTitle } from "./results";

/** The items of a turn, one under another; `highlight` marks the ones a failed check points at. */
export function ConversationView({ items, highlight }: { items: ConversationItem[]; highlight: Set<string> }) {
    if (items.length === 0) return <Text c="dimmed">No output.</Text>;
    return (
        <Stack gap="xs">
            {items.map((item, index) => {
                const marked = highlight.has(itemKey(item));
                return (
                    // Items have no id and may repeat, so the position is their identity.
                    <Paper key={index} withBorder p="xs" style={marked ? { borderColor: "var(--mantine-color-red-6)" } : undefined}>
                        <Badge variant="light" color={marked ? "red" : "gray"} mb={4}>
                            {itemTitle(item)}
                            {marked ? " · evidence" : ""}
                        </Badge>
                        <Code block>{itemBody(item)}</Code>
                    </Paper>
                );
            })}
        </Stack>
    );
}
