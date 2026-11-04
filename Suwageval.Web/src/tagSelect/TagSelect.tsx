import { MultiSelect } from "@mantine/core";

import { tagOptions } from "./tagOptions";

const FIELD_WIDTH = 480;
const DROPDOWN_HEIGHT = 280;

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
    const options = tagOptions(tags, selected);
    if (options.length === 0) return null;
    return (
        <MultiSelect
            label={label}
            placeholder={selected.length === 0 ? placeholder : undefined}
            data={options}
            value={[...selected]}
            onChange={onChange}
            searchable
            clearable
            nothingFoundMessage="No such tag"
            maxDropdownHeight={DROPDOWN_HEIGHT}
            maw={FIELD_WIDTH}
            w="100%"
        />
    );
}
