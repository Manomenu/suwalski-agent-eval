import { Chip, Tooltip } from "@mantine/core";

const HELP_WIDTH = 320;

// Why: the chip sits in a span so the tooltip has an element to hang on
export function ToggleChip({
    label,
    checked,
    onChange,
    help,
    disabled = false,
}: {
    label: string;
    checked: boolean;
    onChange: (checked: boolean) => void;
    help?: string;
    disabled?: boolean;
}) {
    const chip = (
        <span>
            <Chip checked={checked} onChange={onChange} disabled={disabled}>
                {label}
            </Chip>
        </span>
    );
    return help === undefined ? (
        chip
    ) : (
        <Tooltip label={help} multiline w={HELP_WIDTH}>
            {chip}
        </Tooltip>
    );
}
