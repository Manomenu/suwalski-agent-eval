import { TagSelect } from "../tagSelect/TagSelect";

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
