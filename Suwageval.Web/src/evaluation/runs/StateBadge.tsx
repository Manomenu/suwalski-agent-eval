import { Badge, Stack, Text, Tooltip } from "@mantine/core";
import type { ReactNode } from "react";

import type { RegressionState } from "./api";
import { stateColor, stateExplanation, stateLabel } from "./regression";

const TOOLTIP_WIDTH = 340;

/** A check's state against its history, with what the state means on hover. */
export function StateBadge({
    state,
    variant,
    children,
}: {
    state: RegressionState;
    variant?: "filled" | "light" | undefined;
    children: ReactNode;
}) {
    return (
        <Tooltip
            multiline
            w={TOOLTIP_WIDTH}
            label={
                <Stack gap={4}>
                    <Text size="sm" fw={600} inherit>
                        {stateLabel(state)}
                    </Text>
                    <Text size="xs" inherit>
                        {stateExplanation(state)}
                    </Text>
                </Stack>
            }
        >
            <Badge color={stateColor(state)} variant={variant ?? "filled"}>
                {children}
            </Badge>
        </Tooltip>
    );
}
