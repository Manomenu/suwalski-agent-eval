import { Chip, Tooltip } from "@mantine/core";

const ARCHIVED_HELP_WIDTH = 320;

/**
 * The one way a list shows its archived items: an "Archived" chip. `help` says what archived means in that list (the tooltip);
 * the chip sits in a span so the tooltip has an element to hang on.
 */
export function ArchivedChip({ checked, help, onChange }: { checked: boolean; help: string; onChange: (checked: boolean) => void }) {
    return (
        <Tooltip label={help} multiline w={ARCHIVED_HELP_WIDTH}>
            <span>
                <Chip checked={checked} onChange={onChange}>
                    Archived
                </Chip>
            </span>
        </Tooltip>
    );
}
