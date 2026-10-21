import { Button, Group } from "@mantine/core";

import { chipTags, toggledTags } from "./tagSelection";

/** A button per tag of the view above its tables: a pressed one narrows the rows to the scenarios that carry it. */
export function TagFilter({
    tags,
    selected,
    onChange,
}: {
    tags: Iterable<string>;
    selected: readonly string[];
    onChange: (tags: string[]) => void;
}) {
    const offered = chipTags(tags, selected);
    if (offered.length === 0) return null;
    return (
        <Group gap="xs" role="group" aria-label="Filter by tag">
            {offered.map((tag) => {
                const pressed = selected.includes(tag);
                return (
                    <Button
                        key={tag}
                        size="compact-sm"
                        radius="xl"
                        variant={pressed ? "filled" : "light"}
                        aria-pressed={pressed}
                        classNames={{ label: "tag-filter-label" }}
                        onClick={() => {
                            onChange(toggledTags(selected, tag));
                        }}
                    >
                        {tag}
                    </Button>
                );
            })}
            {selected.length > 0 && (
                <Button
                    size="compact-sm"
                    variant="subtle"
                    onClick={() => {
                        onChange([]);
                    }}
                >
                    Clear filter
                </Button>
            )}
        </Group>
    );
}
