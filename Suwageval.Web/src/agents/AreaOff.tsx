import { Alert } from "@maniumek/suwfrontkit";

import { areaOffText, type Area } from "./features";

export function AreaOff({ area }: { area: Area }) {
    return (
        <Alert>
            <p className="font-semibold">{area === "evaluation" ? "Evaluation is off" : "Monitoring is off"}</p>
            <p>{areaOffText(area)}</p>
        </Alert>
    );
}
