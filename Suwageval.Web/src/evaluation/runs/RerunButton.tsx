import { Alert, Button } from "@maniumek/suwfrontkit";
import { useState } from "react";

import { Failure } from "../../api/Failure";
import { runRequestLike, startRun, type RunSummary } from "./api";
import { runHref } from "../../routes/links";

export function RerunButton({ run }: { run: RunSummary }) {
    const [busy, setBusy] = useState(false);
    const [failed, setFailed] = useState<{ error: unknown } | null>(null);

    const start = () => {
        setBusy(true);
        setFailed(null);
        startRun(runRequestLike(run)).then(
            (created) => {
                window.location.assign(runHref(created.runId));
            },
            (failure: unknown) => {
                setFailed({ error: failure });
                setBusy(false);
            },
        );
    };

    return (
        <div className="flex flex-col items-start gap-2.5">
            <Button onClick={start} disabled={busy}>
                {busy ? "Starting…" : "Run again"}
            </Button>
            {failed !== null && (
                <Alert variant="destructive">
                    <p className="font-semibold">The run could not be started</p>
                    <Failure error={failed.error} />
                </Alert>
            )}
        </div>
    );
}
