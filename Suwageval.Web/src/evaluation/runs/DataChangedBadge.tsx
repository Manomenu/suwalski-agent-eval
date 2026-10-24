import { Badge, Tooltip } from "@mantine/core";

import { DATA_CHANGED_HELP } from "./regression";

/** The mark of a failed check whose last passing run was on other retrieval data. */
export function DataChangedBadge() {
    return (
        <Tooltip label={DATA_CHANGED_HELP} multiline w={300}>
            <Badge color="cyan" variant="light">
                data changed
            </Badge>
        </Tooltip>
    );
}
