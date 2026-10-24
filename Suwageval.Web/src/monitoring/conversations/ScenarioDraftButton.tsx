import { ActionIcon, Alert, Button, Code, CopyButton, Group, Loader, Modal, Select, Stack, Text, Tooltip } from "@mantine/core";
import { IconCheck, IconCopy } from "@tabler/icons-react";
import { useState } from "react";

import { useRequest } from "../../api/useRequest";
import { fetchScenarioDraft } from "./api";
import { replayOptions, SCENARIO_DRAFT_HELP, SIMULATED_DRAFT_HELP, untilTurnOf } from "./conversations";

/**
 * "Scenario YAML": the conversation as a scenario file to copy into the agent's repository, with the file's path. Filled red when a
 * monitoring check failed on the conversation: a failure is what is most worth keeping as a scenario.
 */
export function ScenarioDraftButton({ conversationId, turns, failed }: { conversationId: string; turns: number; failed: boolean }) {
    const [opened, setOpened] = useState(false);
    const [replay, setReplay] = useState<string | null>("");
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
                {opened && (
                    <Stack gap="sm">
                        {turns > 1 && (
                            <Select label="Replay" data={replayOptions(turns)} value={replay} onChange={setReplay} allowDeselect={false} />
                        )}
                        <Draft conversationId={conversationId} untilTurn={untilTurnOf(replay)} />
                    </Stack>
                )}
            </Modal>
        </>
    );
}

function Draft({ conversationId, untilTurn }: { conversationId: string; untilTurn: number | null }) {
    const draft = useRequest(JSON.stringify([conversationId, untilTurn]), (signal) =>
        fetchScenarioDraft(conversationId, untilTurn, signal),
    );
    if (draft.kind === "loading") return <Loader aria-label="Loading the draft" />;
    if (draft.kind === "error") return <Text c="red">{draft.error.message}</Text>;
    const { path, yaml, notice } = draft.value;
    return (
        <Stack gap="sm">
            <Text size="sm">{untilTurn === null ? SCENARIO_DRAFT_HELP : SIMULATED_DRAFT_HELP}</Text>
            {notice != null && (
                <Alert color="yellow" title="Write the simulated user yourself">
                    {notice}
                </Alert>
            )}
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
