import { Alert, Button, Group, Loader, Modal, Stack, Text, TextInput } from "@mantine/core";
import { useState } from "react";

import { confirmsDeletion, deletionSummary } from "./deletion";
import { useRequest } from "../api/useRequest";

/**
 * Deleting an archived version or run group: says what goes with it (the server counts), and the button waits until its id or name
 * is typed. The server's refusal (a run still queued or running) is shown in the modal.
 */
export function DeleteModal({
    noun,
    title,
    subject,
    typedLabel,
    expected,
    loadPreview,
    remove,
    onClose,
    onDeleted,
}: {
    /** "version" or "run group": names what is deleted in the messages. */
    noun: string;
    title: string;
    /** What the first line calls the thing: its label, id or name. */
    subject: string;
    typedLabel: string;
    /** What must be typed to confirm. */
    expected: string;
    loadPreview: (signal: AbortSignal) => Promise<{ runs: number; samples: number; verdicts: number }>;
    remove: () => Promise<void>;
    onClose: () => void;
    /** Called once the server deleted it. */
    onDeleted: () => void;
}) {
    const [typed, setTyped] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const preview = useRequest(expected, loadPreview);

    const confirm = () => {
        setBusy(true);
        setError(null);
        remove().then(
            () => {
                setBusy(false);
                onDeleted();
            },
            (failure: unknown) => {
                setError(failure instanceof Error ? failure.message : `The ${noun} could not be deleted.`);
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
            title={title}
        >
            <Stack>
                <Text>
                    {noun[0]?.toUpperCase()}
                    {noun.slice(1)} <b>{subject}</b> is deleted for good, with everything that belongs to it.
                </Text>
                {preview.kind === "loading" && <Loader size="sm" />}
                {preview.kind === "loaded" && <Text>{deletionSummary(preview.value)}.</Text>}
                {preview.kind === "error" && (
                    <Alert color="red" title={`The ${noun} cannot be deleted`}>
                        {preview.error.message}
                    </Alert>
                )}
                <TextInput
                    label={typedLabel}
                    description={expected}
                    value={typed}
                    onChange={(event) => {
                        setTyped(event.currentTarget.value);
                    }}
                />
                {error !== null && (
                    <Alert color="red" title={`The ${noun} could not be deleted`}>
                        {error}
                    </Alert>
                )}
                <Group justify="flex-end">
                    <Button variant="default" disabled={busy} onClick={onClose}>
                        Cancel
                    </Button>
                    <Button color="red" loading={busy} disabled={busy || !confirmsDeletion(typed, expected)} onClick={confirm}>
                        Delete
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
