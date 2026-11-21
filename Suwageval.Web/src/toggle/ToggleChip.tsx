import { ToggleChip as Chip, Tooltip } from "@maniumek/suwfrontkit";

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
    return (
        <Tooltip label={help} disabled={help === undefined}>
            <Chip pressed={checked} onPressedChange={onChange} disabled={disabled}>
                {label}
            </Chip>
        </Tooltip>
    );
}
