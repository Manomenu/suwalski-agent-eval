import {
    ActionIcon,
    Alert,
    Button,
    Code,
    CopyButton,
    Group,
    Loader,
    Modal,
    NumberInput,
    Select,
    Stack,
    Text,
    Tooltip,
} from "@mantine/core";
import { IconCheck, IconCopy } from "@tabler/icons-react";
import { useState } from "react";

import { useRequest } from "../../api/useRequest";
import { fetchScenarioDraft } from "./api";
import type { ConversationItem } from "../../conversation/items";
import { ToggleChip } from "../../toggle/ToggleChip";
import {
    DRAFT_CUTS,
    draftCutOf,
    draftTurns,
    MAX_DRAFT_TURNS,
    messageOptions,
    NOTHING_TO_SIMULATE,
    SCENARIO_DRAFT_HELP,
    SIMULATE_REST_HELP,
    type DraftCut,
} from "./conversations";

/**
 * "Scenario YAML": the conversation as a scenario file to copy into the agent's repository, with the file's path. Filled red when a
 * monitoring check failed on the conversation: a failure is what is most worth keeping as a scenario. Copied up to a message, the rest
 * can be simulated ("Simulate the rest"): a model describes the customer, so the draft is generated on a click, never on every change.
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
    const [simulate, setSimulate] = useState(false);
    const [turns, setTurns] = useState<number | null>(null);
    // The request generated last, by the button: null until clicked, and again after anything it was made of changes.
    const [generated, setGenerated] = useState<number | null>(null);
    const options = cut === "end" ? [] : messageOptions(items, cut);
    // The last message of the chosen side until one is picked.
    const picked = ordinal ?? options.at(-1)?.value ?? "1";
    const turnsLeft = cut === "end" ? 0 : draftTurns(items, cut, Number(picked));
    const simulating = simulate && turnsLeft > 0;
    const shownTurns = turns ?? turnsLeft;
    const changed = () => {
        setGenerated(null);
    };
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
                                setTurns(null);
                                changed();
                            }}
                            allowDeselect={false}
                        />
                        {cut !== "end" && (
                            <Select
                                label={cut === "user" ? "Last user message" : "Last agent message"}
                                data={options}
                                value={picked}
                                onChange={(value) => {
                                    setOrdinal(value);
                                    setTurns(null);
                                    changed();
                                }}
                                allowDeselect={false}
                            />
                        )}
                        {cut !== "end" && (
                            <Group gap="sm" align="flex-end">
                                <ToggleChip
                                    label="Simulate the rest"
                                    checked={simulating}
                                    disabled={turnsLeft === 0}
                                    help={turnsLeft === 0 ? NOTHING_TO_SIMULATE : SIMULATE_REST_HELP}
                                    onChange={(checked) => {
                                        setSimulate(checked);
                                        changed();
                                    }}
                                />
                                {simulating && (
                                    <>
                                        <NumberInput
                                            label="Turns"
                                            w={100}
                                            min={1}
                                            max={MAX_DRAFT_TURNS}
                                            clampBehavior="strict"
                                            allowDecimal={false}
                                            value={shownTurns}
                                            onChange={(value) => {
                                                setTurns(typeof value === "number" ? value : null);
                                                changed();
                                            }}
                                        />
                                        <Button
                                            onClick={() => {
                                                setGenerated(Date.now());
                                            }}
                                        >
                                            Generate scenario
                                        </Button>
                                    </>
                                )}
                            </Group>
                        )}
                        {!simulating && <Draft conversationId={conversationId} cut={cut} ordinal={Number(picked)} turns={null} run={0} />}
                        {simulating && generated !== null && (
                            <Draft conversationId={conversationId} cut={cut} ordinal={Number(picked)} turns={shownTurns} run={generated} />
                        )}
                    </Stack>
                )}
            </Modal>
        </>
    );
}

/** The draft, asked once per what it is made of; `run` asks a generated one again (the model may answer differently). */
function Draft({
    conversationId,
    cut,
    ordinal,
    turns,
    run,
}: {
    conversationId: string;
    cut: DraftCut;
    ordinal: number;
    /** The simulated rest's turns; null, copied only. */
    turns: number | null;
    run: number;
}) {
    const draft = useRequest(JSON.stringify([conversationId, cut, ordinal, turns, run]), (signal) =>
        fetchScenarioDraft(conversationId, cut, ordinal, turns, signal),
    );
    if (draft.kind === "loading") return <Loader aria-label="Loading the draft" />;
    if (draft.kind === "error") return <Text c="red">{draft.error.message}</Text>;
    const { path, yaml, notice } = draft.value;
    return (
        <Stack gap="sm">
            <Text size="sm">{SCENARIO_DRAFT_HELP}</Text>
            {notice && <Alert color="yellow">{notice}</Alert>}
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
