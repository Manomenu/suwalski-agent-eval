import { TagSelect } from "../tagSelect/TagSelect";

/** The tag filter above a view's tables: a picked tag narrows the rows to the scenarios that carry it, any picked tag is enough. */
export function TagFilter({
    tags,
    selected,
    onChange,
}: {
    tags: Iterable<string>;
    selected: readonly string[];
    onChange: (tags: string[]) => void;
}) {
    return (
        <TagSelect
            label="Filter by tag"
            placeholder="Every scenario — type to find a tag"
            tags={tags}
            selected={selected}
            onChange={onChange}
        />
    );
}
