import { ActionIcon, CopyButton, Group, Stack, Text, Tooltip } from "@mantine/core";
import { IconCheck, IconCopy } from "@tabler/icons-react";

import { shortDataVersion } from "./shortDataVersion";

const DESCRIPTION_LINES = 2;

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
            {/* Why: as wide as its text, not the column, so the tooltip is placed next to the words */}
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
