import { MultiSelect } from "@mantine/core";

import { offeredTags } from "./tagSelection";

/** The field's width: wide enough for a few picked tags, never wider than the page. */
const TAG_FILTER_WIDTH = 480;
/** How tall the list of tags may grow before it scrolls. */
const DROPDOWN_HEIGHT = 280;

/**
 * The tag filter above a view's tables: a field that searches the view's tags as you type and keeps the picked ones as pills.
 * A picked tag narrows the rows to the scenarios that carry it; any of the picked tags is enough.
 */
export function TagFilter({
    tags,
    selected,
    onChange,
}: {
    tags: Iterable<string>;
    selected: readonly string[];
    onChange: (tags: string[]) => void;
}) {
    const offered = offeredTags(tags, selected);
    if (offered.length === 0) return null;
    return (
        <MultiSelect
            label="Filter by tag"
            placeholder={selected.length === 0 ? "Every scenario — type to find a tag" : undefined}
            data={offered}
            value={[...selected]}
            onChange={onChange}
            searchable
            clearable
            nothingFoundMessage="No such tag"
            maxDropdownHeight={DROPDOWN_HEIGHT}
            maw={TAG_FILTER_WIDTH}
            w="100%"
        />
    );
}
