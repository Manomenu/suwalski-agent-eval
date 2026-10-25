import { Chip, Tooltip } from "@mantine/core";

const HELP_WIDTH = 320;

/**
 * The one on/off control of the app: a chip with its label (a filter such as "Archived", a view such as "Hide passed checks", an option
 * such as "Simulate the rest"). `help`, when given, says on hover what it does; the chip sits in a span so the tooltip has an element to
 * hang on.
 */
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
