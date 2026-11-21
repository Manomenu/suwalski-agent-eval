import { Badge, Tooltip } from "@maniumek/suwfrontkit";

import { DATA_CHANGED_HELP } from "./regression";

export function DataChangedBadge() {
    return (
        <Tooltip label={DATA_CHANGED_HELP}>
            <Badge variant="info">data changed</Badge>
        </Tooltip>
    );
}
