import { Tooltip } from "@mantine/core";
import { IconRobot } from "@tabler/icons-react";

import { modelJudgedText, modelJudgedTooltip } from "./modelJudged";

export function ModelJudgedIcon({ model }: { model?: string | null | undefined }) {
    return (
        <Tooltip label={modelJudgedTooltip(model)}>
            <IconRobot size={18} color="var(--mantine-color-mauve-4)" aria-label={modelJudgedText(model)} role="img" />
        </Tooltip>
    );
}
