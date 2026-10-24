import { ActionIcon, CopyButton, Group, Stack, Text, Tooltip } from "@mantine/core";
import { IconCheck, IconCopy } from "@tabler/icons-react";

import { shortDataVersion } from "./shortDataVersion";

/** How many lines a data description takes before it is cut (the whole text is on hover). */
const DESCRIPTION_LINES = 2;

/**
 * A run's retrieval data: its data version by its start (the whole value on hover, and a button that copies it — to look it up
 * in the agent's index) and its description cut to a couple of lines. Its size never depends on how long either is, so a header of
 * many columns keeps one height.
 */
export function DataVersion({
    dataVersion,
    description,
    size = "xs",
}: {
    dataVersion: string;
    description?: string | null | undefined;
    size?: "xs" | "sm";
}) {
    const text = description?.trim();
    return (
        <Stack gap={0}>
            <Group gap={4} wrap="nowrap">
                <Text size={size} fw="normal">
                    Data{" "}
                    <Tooltip label={dataVersion} multiline maw={360} style={{ overflowWrap: "anywhere" }}>
                        <Text span inherit style={{ cursor: "help" }}>
                            {shortDataVersion(dataVersion)}
                        </Text>
                    </Tooltip>
                </Text>
                <CopyButton value={dataVersion}>
                    {({ copied, copy }) => (
                        <Tooltip label={copied ? "Copied" : "Copy the data version"}>
                            <ActionIcon
                                size="xs"
                                variant="subtle"
                                className="no-print"
                                color={copied ? "teal" : "gray"}
                                aria-label="Copy the data version"
                                onClick={copy}
                            >
                                {copied ? <IconCheck size={12} /> : <IconCopy size={12} />}
                            </ActionIcon>
                        </Tooltip>
                    )}
                </CopyButton>
            </Group>
            {/* As wide as its text, not the column: the tooltip is placed against it, so it shows up next to the words. */}
            {text ? (
                <Tooltip label={text} multiline maw={360}>
                    <Text size={size} fw="normal" c="dimmed" lineClamp={DESCRIPTION_LINES} w="fit-content" maw="100%">
                        {text}
                    </Text>
                </Tooltip>
            ) : null}
        </Stack>
    );
}
