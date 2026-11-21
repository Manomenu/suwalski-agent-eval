import { Tooltip } from "@maniumek/suwfrontkit";
import { IconWaveSine } from "@tabler/icons-react";

const ICON_SIZE = 18;

export const UNSTABLE_EXPLANATION =
    "An unstable check passed in some runs of one agent version and failed in others. Nothing in the agent changed between those runs: the model simply answered differently. So a failure of this check may be noise, not a regression. Read the failing answer; if the check is right, raise the scenario's samples so its pass rate is measured over several answers. Re-checks do not count.";

export function UnstableCheckIcon({ label }: { label?: string }) {
    if (label === undefined) return <IconWaveSine size={ICON_SIZE} className="text-warning" aria-hidden />;
    return (
        <Tooltip
            label={
                <div className="flex flex-col gap-1">
                    <p className="text-sm font-semibold">{label}</p>
                    <p className="text-xs">{UNSTABLE_EXPLANATION}</p>
                </div>
            }
        >
            <IconWaveSine size={ICON_SIZE} className="text-warning" aria-label="Unstable check" role="img" />
        </Tooltip>
    );
}
