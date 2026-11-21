import { Label, MultiSelect } from "@maniumek/suwfrontkit";
import { useId } from "react";

import { tagOptions } from "../../tagFilter/tagOptions";
import { useTagFilter } from "../../tagFilter/useTagFilter";
import { type RunPassRates } from "./api";
import { PassRateTrend } from "./PassRateTrend";
import { DEFAULT_TAG_LINES, recentTags, seriesTags } from "./trend";
import { TurnTimeTrend } from "./TurnTimeTrend";

const SERIES_PARAM = "series";
const LABEL = "Tags in the charts";

export function Trends({ points }: { points: RunPassRates[] }) {
    const [picked, pick] = useTagFilter(SERIES_PARAM);
    const recent = recentTags(points, DEFAULT_TAG_LINES);
    const id = useId();
    const choices = tagOptions(seriesTags(points), picked);
    return (
        <div className="flex flex-col gap-5">
            {choices.length > 0 && (
                <div className="flex w-full max-w-[480px] flex-col gap-1">
                    <Label htmlFor={id}>{LABEL}</Label>
                    <MultiSelect
                        id={id}
                        label={LABEL}
                        placeholder={`The ${String(DEFAULT_TAG_LINES)} most recent tags — type to find another`}
                        emptyLabel="No such tag"
                        options={choices.map((tag) => ({ value: tag, label: tag }))}
                        value={picked}
                        onValueChange={pick}
                    />
                </div>
            )}
            <PassRateTrend points={points} picked={picked} recent={recent} />
            <TurnTimeTrend points={points} picked={picked} recent={recent} />
        </div>
    );
}
