import { ActionIcon, Button, Code, CopyButton, Group, Loader, Modal, Stack, Text, Tooltip } from "@mantine/core";
import { IconCheck, IconCopy } from "@tabler/icons-react";
import { useState } from "react";

import { useRequest } from "../../api/useRequest";
import { fetchScenarioDraft } from "./api";
import { SCENARIO_DRAFT_HELP } from "./conversations";

/**
 * "Scenario YAML": the conversation as a scenario file to copy into the agent's repository, with the file's path. Filled red when a
 * monitoring check failed on the conversation: a failure is what is most worth keeping as a scenario.
 */
export function ScenarioDraftButton({ conversationId, failed }: { conversationId: string; failed: boolean }) {
    const [opened, setOpened] = useState(false);
    return (
        <>
            <Button
                variant={failed ? "filled" : "light"}
                color={failed ? "red" : "mauve"}
                onClick={() => {
                    setOpened(true);
                }}
            >
                Scenario YAML
            </Button>
            <Modal
                opened={opened}
                onClose={() => {
                    setOpened(false);
                }}
                title="This conversation as a scenario"
                size="xl"
            >
                {opened && <Draft conversationId={conversationId} />}
            </Modal>
        </>
    );
}

function Draft({ conversationId }: { conversationId: string }) {
    const draft = useRequest(conversationId, (signal) => fetchScenarioDraft(conversationId, signal));
    if (draft.kind === "loading") return <Loader aria-label="Loading the draft" />;
    if (draft.kind === "error") return <Text c="red">{draft.error.message}</Text>;
    const { path, yaml } = draft.value;
    return (
        <Stack gap="sm">
            <Text size="sm">{SCENARIO_DRAFT_HELP}</Text>
            <Group gap="xs" wrap="nowrap">
                <Code aria-label="File path">{path}</Code>
                <Copy value={path} label="Copy the path" />
            </Group>
            <Group justify="flex-end">
                <Copy value={yaml} label="Copy the YAML" />
            </Group>
            <Code block aria-label="Scenario YAML">
                {yaml}
            </Code>
        </Stack>
    );
}

function Copy({ value, label }: { value: string; label: string }) {
    return (
        <CopyButton value={value}>
            {({ copied, copy }) => (
                <Tooltip label={copied ? "Copied" : label}>
                    <ActionIcon variant="subtle" onClick={copy} aria-label={label}>
                        {copied ? <IconCheck size={16} /> : <IconCopy size={16} />}
                    </ActionIcon>
                </Tooltip>
            )}
        </CopyButton>
    );
}
