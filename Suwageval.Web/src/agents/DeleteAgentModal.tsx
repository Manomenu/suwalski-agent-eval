import { Alert, Button, Group, Loader, Modal, Stack, Text, TextInput } from "@mantine/core";
import { useState } from "react";

import { deleteAgent, fetchAgentDeletion } from "./api";
import { agentDeletionSummary, confirmsDeletion } from "./deletion";
import { useLoaded } from "./useLoaded";

/**
 * Deleting an agent (one registered by mistake): says what goes with it (the server counts), and the button waits until the agent's
 * name is typed. The server's refusal (a run still queued or running) is shown in the modal.
 */
export function DeleteAgentModal({ name, onClose, onDeleted }: { name: string; onClose: () => void; onDeleted: () => void }) {
    const [typed, setTyped] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const preview = useLoaded(name, (signal) => fetchAgentDeletion(name, signal));

    const confirm = () => {
        setBusy(true);
        setError(null);
        deleteAgent(name).then(
            () => {
                setBusy(false);
                onDeleted();
            },
            (failure: unknown) => {
                setError(failure instanceof Error ? failure.message : "The agent could not be deleted.");
                setBusy(false);
            },
        );
    };

    return (
        <Modal
            opened
            onClose={() => {
                if (!busy) onClose();
            }}
            title="Delete this agent?"
        >
            <Stack>
                <Text>
                    Agent <b>{name}</b> is deleted for good, with everything that belongs to it. Files in its repository are not touched,
                    but the next push registers it again, empty.
                </Text>
                {preview.kind === "loading" && <Loader size="sm" />}
                {preview.kind === "loaded" && <Text>{agentDeletionSummary(preview.value)}.</Text>}
                {preview.kind === "error" && (
                    <Alert color="red" title="The agent cannot be deleted">
                        {preview.error.message}
                    </Alert>
                )}
                <TextInput
                    label="Type the agent name to confirm"
                    value={typed}
                    onChange={(event) => {
                        setTyped(event.currentTarget.value);
                    }}
                />
                {error !== null && (
                    <Alert color="red" title="The agent could not be deleted">
                        {error}
                    </Alert>
                )}
                <Group justify="flex-end">
                    <Button variant="default" disabled={busy} onClick={onClose}>
                        Cancel
                    </Button>
                    <Button color="red" loading={busy} disabled={busy || !confirmsDeletion(typed, name)} onClick={confirm}>
                        Delete
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
