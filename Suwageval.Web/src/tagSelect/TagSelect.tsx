import { Label, MultiSelect } from "@maniumek/suwfrontkit";
import { useId } from "react";

import { tagOptions } from "./tagOptions";

export function TagSelect({
    label,
    placeholder,
    tags,
    selected,
    onChange,
}: {
    label: string;
    placeholder: string;
    tags: Iterable<string>;
    selected: readonly string[];
    onChange: (tags: string[]) => void;
}) {
    const id = useId();
    const options = tagOptions(tags, selected);
    if (options.length === 0) return null;
    return (
        <div className="flex w-full max-w-[480px] flex-col gap-1">
            <Label htmlFor={id}>{label}</Label>
            <MultiSelect
                id={id}
                label={label}
                placeholder={placeholder}
                emptyLabel="No such tag"
                options={options.map((tag) => ({ value: tag, label: tag }))}
                value={selected}
                onValueChange={onChange}
            />
        </div>
    );
}
