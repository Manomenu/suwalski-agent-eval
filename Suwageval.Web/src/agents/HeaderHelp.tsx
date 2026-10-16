import { Text, Tooltip } from "@mantine/core";

const HELP_WIDTH = 320;

/** A column header that explains itself on hover: dotted underline, the explanation in a tooltip. */
export function HeaderHelp({ label, help }: { label: string; help: string }) {
    return (
        <Tooltip label={help} multiline w={HELP_WIDTH}>
            <Text span inherit td="underline" style={{ textDecorationStyle: "dotted", cursor: "help" }}>
                {label}
            </Text>
        </Tooltip>
    );
}
