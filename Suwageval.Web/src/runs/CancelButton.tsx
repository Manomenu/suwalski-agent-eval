import { Alert, Button, Group, Modal, Stack, Text } from "@mantine/core";
import { useState } from "react";

import { cancelRun, type RunSummary } from "./api";
import { canCancel } from "./results";

/**
 * "Cancel run", for a queued or running run: asks first, then asks the server to stop it. The page's own refresh then shows
 * "cancelling…" until the run is cancelled. The server's refusal is shown as it came.
 */
export function CancelButton({ run }: { run: Pick<RunSummary, "id" | "status" | "cancelRequested"> }) {
    const [asking, setAsking] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    if (!canCancel(run)) return null;

    const confirm = () => {
        setBusy(true);
        setError(null);
        cancelRun(run.id).then(
            () => {
                setAsking(false);
                setBusy(false);
            },
            (failure: unknown) => {
                setError(failure instanceof Error ? failure.message : "The run could not be cancelled.");
                setBusy(false);
            },
        );
    };

    return (
        <Stack gap="xs" align="flex-start">
            <Button
                variant="default"
                onClick={() => {
                    setAsking(true);
                }}
            >
                Cancel run
            </Button>
            <Modal
                opened={asking}
                onClose={() => {
                    if (!busy) setAsking(false);
                }}
                title="Cancel this run?"
            >
                <Stack>
                    <Text>Samples already in progress finish; no new sample starts, and the run ends as cancelled.</Text>
                    {error && (
                        <Alert color="red" title="The run could not be cancelled">
                            {error}
                        </Alert>
                    )}
                    <Group justify="flex-end">
                        <Button
                            variant="default"
                            disabled={busy}
                            onClick={() => {
                                setAsking(false);
                            }}
                        >
                            Keep running
                        </Button>
                        <Button color="red" onClick={confirm} loading={busy} disabled={busy}>
                            Cancel run
                        </Button>
                    </Group>
                </Stack>
            </Modal>
        </Stack>
    );
}
