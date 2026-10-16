import { Tooltip } from "@mantine/core";
import { IconRobot } from "@tabler/icons-react";

/** Marks a check a model judges: its verdict and its reason come from the model, not from a rule. */
export function ModelJudgedIcon() {
    return (
        <Tooltip label="Judged by a model: not deterministic">
            <IconRobot size={18} color="var(--mantine-color-mauve-4)" aria-label="Judged by a model" role="img" />
        </Tooltip>
    );
}
