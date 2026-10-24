import { Alert } from "@mantine/core";

import { areaOffText, type Area } from "./features";

/** What the page of a switched-off area shows instead of its content. */
export function AreaOff({ area }: { area: Area }) {
    return (
        <Alert color="gray" title={area === "evaluation" ? "Evaluation is off" : "Monitoring is off"}>
            {areaOffText(area)}
        </Alert>
    );
}
