import { MultiSelect } from "@mantine/core";

import { tagOptions } from "./tagOptions";

/** The field's width: wide enough for a few picked tags, never wider than the page. */
const FIELD_WIDTH = 480;
/** How tall the list of tags may grow before it scrolls. */
const DROPDOWN_HEIGHT = 280;

/**
 * Picking tags out of a list that may be long: a field that searches the tags as you type and keeps the picked ones as pills.
 * What picking means is the caller's (`label` says it): which rows a view keeps, which lines a chart draws. Nothing picked is
 * the caller's "every tag". Nothing is shown while there is no tag to pick.
 */
export function TagSelect({
    label,
    placeholder,
    tags,
    selected,
    onChange,
}: {
    label: string;
    /** Said while nothing is picked: what the view shows then. */
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
