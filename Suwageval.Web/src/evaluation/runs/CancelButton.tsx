import { Alert, Button, ConfirmDialog } from "@maniumek/suwfrontkit";
import { useState } from "react";

import { Failure } from "../../api/Failure";
import { cancelRun, type RunSummary } from "./api";
import { canCancel } from "./results";

export function CancelButton({ run }: { run: Pick<RunSummary, "id" | "status" | "cancelRequested"> }) {
    const [asking, setAsking] = useState(false);
    const [failed, setFailed] = useState<{ error: unknown } | null>(null);

    if (!canCancel(run)) return null;

    const confirm = async () => {
        setFailed(null);
        try {
            await cancelRun(run.id);
        } catch (failure: unknown) {
            setFailed({ error: failure });
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
                    {failed !== null && (
                        <Alert variant="destructive">
                            <p className="font-semibold">The run could not be cancelled</p>
                            <Failure error={failed.error} />
                        </Alert>
                    )}
                </div>
            </ConfirmDialog>
        </div>
    );
}
