import { Stack, Text, Title } from "@mantine/core";

import { ConversationItems } from "../../conversation/ConversationItems";
import type { ConversationItem } from "./api";
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
    if (!simulated) return <ConversationItems items={items} highlight={highlight} />;
    return (
        <Stack gap="md">
            {simulationTurns(items).map((turn, index) => (
                // Turns have no id; their position is their number.
                <Stack key={index} gap="xs">
                    <Title order={5}>Turn {index + 1}</Title>
                    <ConversationItems items={turn} highlight={highlight} simulatedUser />
                </Stack>
            ))}
        </Stack>
    );
}
