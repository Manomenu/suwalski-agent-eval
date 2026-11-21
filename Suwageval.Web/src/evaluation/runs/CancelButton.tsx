import { Alert, Button, ConfirmDialog } from "@maniumek/suwfrontkit";
import { useState } from "react";

import { cancelRun, type RunSummary } from "./api";
import { canCancel } from "./results";

export function CancelButton({ run }: { run: Pick<RunSummary, "id" | "status" | "cancelRequested"> }) {
    const [asking, setAsking] = useState(false);
    const [error, setError] = useState<string | null>(null);

    if (!canCancel(run)) return null;

    const confirm = async () => {
        setError(null);
        try {
            await cancelRun(run.id);
        } catch (failure: unknown) {
            setError(failure instanceof Error ? failure.message : "The run could not be cancelled.");
            throw failure;
        }
    };

    return (
        <div className="flex flex-col items-start gap-2.5">
            <Button
                variant="outline"
                onClick={() => {
                    setAsking(true);
                }}
            >
                Cancel run
            </Button>
            <ConfirmDialog
                open={asking}
                onOpenChange={setAsking}
                title="Cancel this run?"
                variant="destructive"
                confirmLabel="Cancel run"
                cancelLabel="Keep running"
                pendingLabel="Working…"
                onConfirm={confirm}
            >
                <div className="flex flex-col gap-4">
                    <p>Samples already in progress finish; no new sample starts, and the run ends as cancelled.</p>
                    {error && (
                        <Alert variant="destructive">
                            <p className="font-semibold">The run could not be cancelled</p>
                            <p>{error}</p>
                        </Alert>
                    )}
                </div>
            </ConfirmDialog>
        </div>
    );
}
