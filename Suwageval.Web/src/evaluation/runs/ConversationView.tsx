import { Badge, Code, Paper, Stack, Text, Title } from "@mantine/core";

import type { ConversationItem } from "./api";
import { itemBody, itemKey, itemTitle } from "./results";
import { simulationTurns } from "./simulation";

/**
 * The items of a turn, one under another; `highlight` marks the ones a failed check points at. A simulation (`simulated`) is shown turn by
 * turn, the simulated user's messages marked: they are not the agent's and no check judges them.
 */
export function ConversationView({
    items,
    highlight,
    simulated = false,
}: {
    items: ConversationItem[];
    highlight: Set<string>;
    simulated?: boolean;
}) {
    if (items.length === 0) return <Text c="dimmed">No output.</Text>;
    if (!simulated) return <Items items={items} highlight={highlight} simulated={false} />;
    return (
        <Stack gap="md">
            {simulationTurns(items).map((turn, index) => (
                // Turns have no id; their position is their number.
                <Stack key={index} gap="xs">
                    <Title order={5}>Turn {index + 1}</Title>
                    <Items items={turn} highlight={highlight} simulated />
                </Stack>
            ))}
        </Stack>
    );
}

function Items({ items, highlight, simulated }: { items: ConversationItem[]; highlight: Set<string>; simulated: boolean }) {
    return (
        <Stack gap="xs">
            {items.map((item, index) => {
                const marked = highlight.has(itemKey(item));
                const simulatedUser = simulated && item.kind === "UserMessage";
                return (
                    // Items have no id and may repeat, so the position is their identity.
                    <Paper key={index} withBorder p="xs" style={marked ? { borderColor: "var(--mantine-color-red-6)" } : undefined}>
                        <Badge variant="light" color={marked ? "red" : simulatedUser ? "blue" : "gray"} mb={4}>
                            {simulatedUser ? "simulated user" : itemTitle(item)}
                            {marked ? " · evidence" : ""}
                        </Badge>
                        <Code block>{itemBody(item)}</Code>
                    </Paper>
                );
            })}
        </Stack>
    );
}
