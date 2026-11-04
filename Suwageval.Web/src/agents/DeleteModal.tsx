import { Alert, Button, Group, Loader, Modal, Stack, Text, TextInput } from "@mantine/core";
import { useState } from "react";

import { confirmsDeletion } from "./deletion";
import { useRequest } from "../api/useRequest";

export function DeleteModal<Preview>({
    noun,
    title,
    subject,
    note,
    typedLabel,
    expected,
    loadPreview,
    summary,
    remove,
    onClose,
    onDeleted,
}: {
    noun: string;
    title: string;
    subject: string;
    note?: string;
    typedLabel: string;
    expected: string;
    loadPreview: (signal: AbortSignal) => Promise<Preview>;
    summary: (preview: Preview) => string;
    remove: () => Promise<void>;
    onClose: () => void;
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
                    {note === undefined ? "" : ` ${note}`}
                </Text>
                {preview.kind === "loading" && <Loader size="sm" />}
                {preview.kind === "loaded" && <Text>{summary(preview.value)}.</Text>}
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
