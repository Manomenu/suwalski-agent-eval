import { Stack } from "@mantine/core";

import { TagSelect } from "../../tagSelect/TagSelect";
import { useTagFilter } from "../../tagFilter/useTagFilter";
import { type RunPassRates } from "./api";
import { PassRateTrend } from "./PassRateTrend";
import { DEFAULT_TAG_LINES, recentTags, seriesTags } from "./trend";
import { TurnTimeTrend } from "./TurnTimeTrend";

/** The address parameter of the tags the charts draw (`&series=a`): apart from `tag`, which filters the runs. */
const SERIES_PARAM = "series";

/** The agent's trend charts under one tag field: the tags picked there (none: the most recent ones) are the lines both charts draw beside "All". */
export function Trends({ points }: { points: RunPassRates[] }) {
    const [picked, pick] = useTagFilter(SERIES_PARAM);
    const recent = recentTags(points, DEFAULT_TAG_LINES);
    return (
        <Stack gap="lg">
            <TagSelect
                label="Tags in the charts"
                placeholder={`The ${String(DEFAULT_TAG_LINES)} most recent tags — type to find another`}
                tags={seriesTags(points)}
                selected={picked}
                onChange={pick}
            />
            <PassRateTrend points={points} picked={picked} recent={recent} />
            <TurnTimeTrend points={points} picked={picked} recent={recent} />
        </Stack>
    );
}
