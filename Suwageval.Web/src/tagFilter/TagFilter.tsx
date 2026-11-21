import { Label, MultiSelect } from "@maniumek/suwfrontkit";
import { useId } from "react";

import { tagOptions } from "./tagOptions";

const LABEL = "Filter by tag";

export function TagFilter({
    tags,
    selected,
    onChange,
}: {
    tags: Iterable<string>;
    selected: readonly string[];
    onChange: (tags: string[]) => void;
}) {
    const id = useId();
    const options = tagOptions(tags, selected);
    if (options.length === 0) return null;
    return (
        <div className="flex w-full max-w-[480px] flex-col gap-1">
            <Label htmlFor={id}>{LABEL}</Label>
            <MultiSelect
                id={id}
                label={LABEL}
                placeholder="Every scenario — type to find a tag"
                emptyLabel="No such tag"
                options={options.map((tag) => ({ value: tag, label: tag }))}
                value={selected}
                onValueChange={onChange}
            />
        </div>
    );
}
