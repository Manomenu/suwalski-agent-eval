import { Stack, Text, Tooltip } from "@mantine/core";
import { IconWaveSine } from "@tabler/icons-react";

const ICON_SIZE = 18;
const ICON_COLOR = "var(--mantine-color-yellow-6)";
const TOOLTIP_WIDTH = 340;

/** What an unstable check is and how to read it, for someone who meets one for the first time. */
export const UNSTABLE_EXPLANATION =
    "An unstable check passed in some runs of one agent version and failed in others. Nothing in the agent changed between those runs: the model simply answered differently. So a failure of this check may be noise, not a regression. Read the failing answer; if the check is right, raise the scenario's samples so its pass rate is measured over several answers. Re-checks do not count.";

/**
 * Marks a check whose verdict differs between runs of the same agent version: its failures may be noise. `label` says how often.
 * Without a `label` the icon is only decoration next to text that already says it (a heading): no tooltip, hidden from screen readers.
 */
export function UnstableCheckIcon({ label }: { label?: string }) {
    if (label === undefined) return <IconWaveSine size={ICON_SIZE} color={ICON_COLOR} aria-hidden />;
    return (
        <Tooltip
            multiline
            w={TOOLTIP_WIDTH}
            label={
                <Stack gap={4}>
                    <Text size="sm" fw={600} inherit>
                        {label}
                    </Text>
                    <Text size="xs" inherit>
                        {UNSTABLE_EXPLANATION}
                    </Text>
                </Stack>
            }
        >
            <IconWaveSine size={ICON_SIZE} color={ICON_COLOR} aria-label="Unstable check" role="img" />
        </Tooltip>
    );
}
