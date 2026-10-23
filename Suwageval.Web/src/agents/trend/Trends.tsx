import { Stack } from "@mantine/core";

import { TagSelect } from "../../tagSelect/TagSelect";
import { useTagFilter } from "../../tagFilter/useTagFilter";
import { type RunPassRates } from "./api";
import { PassRateTrend } from "./PassRateTrend";
import { seriesTags } from "./trend";
import { TurnTimeTrend } from "./TurnTimeTrend";

/** The address parameter of the tags the charts draw (`&series=a`): apart from `tag`, which filters the runs. */
const SERIES_PARAM = "series";

/** The agent's trend charts under one tag field: the tags picked there are the lines both charts draw beside "All". */
export function Trends({ points }: { points: RunPassRates[] }) {
    const [picked, pick] = useTagFilter(SERIES_PARAM);
    return (
        <Stack gap="lg">
            <TagSelect
                label="Tags in the charts"
                placeholder="Every tag — type to find one"
                tags={seriesTags(points)}
                selected={picked}
                onChange={pick}
            />
            <PassRateTrend points={points} picked={picked} />
            <TurnTimeTrend points={points} picked={picked} />
        </Stack>
    );
}
