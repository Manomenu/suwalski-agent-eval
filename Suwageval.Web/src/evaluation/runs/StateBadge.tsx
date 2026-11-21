import { Badge, Tooltip } from "@maniumek/suwfrontkit";
import type { ReactNode } from "react";

import type { RegressionState } from "./api";
import { stateColor, stateExplanation, stateLabel } from "./regression";

export function StateBadge({ state, children }: { state: RegressionState; children: ReactNode }) {
    return (
        <Tooltip
            label={
                <div className="flex flex-col gap-1">
                    <p className="text-sm font-semibold">{stateLabel(state)}</p>
                    <p className="text-xs">{stateExplanation(state)}</p>
                </div>
            }
        >
            <Badge variant={stateColor(state)}>{children}</Badge>
        </Tooltip>
    );
}
