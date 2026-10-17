import { Alert, Button, Group, Loader, Modal, Stack, Text, TextInput } from "@mantine/core";
import { useState } from "react";

import { deleteVersion, fetchVersionDeletion } from "./api";
import { confirmsDeletion, deletionSummary } from "./deletion";
import { useLoaded } from "./useLoaded";

/**
 * Deleting an archived version: says what goes with it (the server counts), and the button waits until the version's id is typed.
 * The server's refusal (a run still queued or running) is shown in the modal.
 */
export function DeleteVersionModal({
    agentName,
    version,
    onClose,
    onDeleted,
}: {
    agentName: string;
    version: { id: string; label?: string | null };
    onClose: () => void;
    /** Called once the server deleted the version. */
    onDeleted: () => void;
}) {
    const [typed, setTyped] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const preview = useLoaded(version.id, (signal) => fetchVersionDeletion(agentName, version.id, signal));

    const confirm = () => {
        setBusy(true);
        setError(null);
        deleteVersion(agentName, version.id).then(
            () => {
                setBusy(false);
                onDeleted();
            },
            (failure: unknown) => {
                setError(failure instanceof Error ? failure.message : "The version could not be deleted.");
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
            title="Delete this version?"
        >
            <Stack>
                <Text>
                    Version <b>{version.label?.trim() ? version.label : version.id}</b> is deleted for good, with everything that belongs to
                    it.
                </Text>
                {preview.kind === "loading" && <Loader size="sm" />}
                {preview.kind === "loaded" && <Text>{deletionSummary(preview.value)}.</Text>}
                {preview.kind === "error" && (
                    <Alert color="red" title="The version cannot be deleted">
                        {preview.error.message}
                    </Alert>
                )}
                <TextInput
                    label="Type the version id to confirm"
                    description={version.id}
                    value={typed}
                    onChange={(event) => {
                        setTyped(event.currentTarget.value);
                    }}
                />
                {error !== null && (
                    <Alert color="red" title="The version could not be deleted">
                        {error}
                    </Alert>
                )}
                <Group justify="flex-end">
                    <Button variant="default" disabled={busy} onClick={onClose}>
                        Cancel
                    </Button>
                    <Button color="red" loading={busy} disabled={busy || !confirmsDeletion(typed, version.id)} onClick={confirm}>
                        Delete
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
