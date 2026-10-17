import { Tooltip } from "@mantine/core";
import { IconRobot } from "@tabler/icons-react";

import { modelJudgedText, modelJudgedTooltip } from "./modelJudged";

/** Marks a check a model judges: its verdict and its reason come from the model, not from a rule. `model` names it when known. */
export function ModelJudgedIcon({ model }: { model?: string | null | undefined }) {
    return (
        <Tooltip label={modelJudgedTooltip(model)}>
            <IconRobot size={18} color="var(--mantine-color-mauve-4)" aria-label={modelJudgedText(model)} role="img" />
        </Tooltip>
    );
}
