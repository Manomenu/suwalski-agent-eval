import { ColorSwatch, Group, Text } from "@mantine/core";

import { swatchColor } from "./chartStyle";

/** The series a chart draws, each with its colour, in one row under the chart. */
export function ChartLegend({ series }: { series: readonly { name: string; color: string }[] }) {
    return (
        <Group gap="md" role="list" aria-label="Series">
            {series.map(({ name, color }) => (
                <Group key={name} gap={6} wrap="nowrap" role="listitem">
                    <ColorSwatch color={`var(--mantine-color-${swatchColor(color)}-5)`} size={10} withShadow={false} />
                    <Text size="sm">{name}</Text>
                </Group>
            ))}
        </Group>
    );
}
