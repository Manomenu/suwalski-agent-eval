import { Tooltip } from "@maniumek/suwfrontkit";

export function HeaderHelp({ label, help }: { label: string; help: string }) {
    return (
        <Tooltip label={help}>
            <span className="cursor-help underline decoration-dotted">{label}</span>
        </Tooltip>
    );
}
