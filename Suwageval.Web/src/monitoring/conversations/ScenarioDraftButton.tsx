import { ActionIcon, Button, Code, CopyButton, Group, Loader, Modal, Select, Stack, Text, Tooltip } from "@mantine/core";
import { IconCheck, IconCopy } from "@tabler/icons-react";
import { useState } from "react";

import { useRequest } from "../../api/useRequest";
import { fetchScenarioDraft } from "./api";
import type { ConversationItem } from "../../conversation/items";
import { DRAFT_CUTS, draftCutOf, messageOptions, SCENARIO_DRAFT_HELP, type DraftCut } from "./conversations";

/**
 * "Scenario YAML": the conversation as a scenario file to copy into the agent's repository, with the file's path. Filled red when a
 * monitoring check failed on the conversation: a failure is what is most worth keeping as a scenario.
 */
export function ScenarioDraftButton({
    conversationId,
    items,
    failed,
}: {
    conversationId: string;
    /** The conversation's items over all its turns, oldest first: the messages to copy up to are picked from them. */
    items: ConversationItem[];
    failed: boolean;
}) {
    const [opened, setOpened] = useState(false);
    const [cut, setCut] = useState<DraftCut>("end");
    const [ordinal, setOrdinal] = useState<string | null>(null);
    const options = cut === "end" ? [] : messageOptions(items, cut);
    // The last message of the chosen side until one is picked.
    const picked = ordinal ?? options.at(-1)?.value ?? "1";
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
                        <Select
                            label="Copy"
                            data={DRAFT_CUTS}
                            value={cut}
                            onChange={(value) => {
                                setCut(draftCutOf(value));
                                setOrdinal(null);
                            }}
                            allowDeselect={false}
                        />
                        {cut !== "end" && (
                            <Select
                                label={cut === "user" ? "Last user message" : "Last agent message"}
                                data={options}
                                value={picked}
                                onChange={setOrdinal}
                                allowDeselect={false}
                            />
                        )}
                        <Draft conversationId={conversationId} cut={cut} ordinal={Number(picked)} />
                    </Stack>
                )}
            </Modal>
        </>
    );
}

function Draft({ conversationId, cut, ordinal }: { conversationId: string; cut: DraftCut; ordinal: number }) {
    const draft = useRequest(JSON.stringify([conversationId, cut, ordinal]), (signal) =>
        fetchScenarioDraft(conversationId, cut, ordinal, signal),
    );
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
