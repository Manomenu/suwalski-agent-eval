import { Tooltip } from "@mantine/core";
import { IconWaveSine } from "@tabler/icons-react";

/** Marks a check whose verdict differs between runs of the same agent version: its failures may be noise. `label` says how often. */
export function UnstableCheckIcon({ label }: { label: string }) {
    return (
        <Tooltip label={label}>
            <IconWaveSine size={18} color="var(--mantine-color-yellow-6)" aria-label="Unstable check" role="img" />
        </Tooltip>
    );
}
