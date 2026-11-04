import { Stack, Text, Title } from "@mantine/core";

import { ConversationItems } from "../../conversation/ConversationItems";
import type { ConversationItem } from "./api";
import { simulationTurns } from "./simulation";

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
                <Stack key={index} gap="xs">
                    <Title order={5}>Turn {index + 1}</Title>
                    <ConversationItems items={turn} highlight={highlight} simulatedUser />
                </Stack>
            ))}
        </Stack>
    );
}
