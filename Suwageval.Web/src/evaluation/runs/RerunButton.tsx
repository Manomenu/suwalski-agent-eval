import { Alert, Button } from "@maniumek/suwfrontkit";
import { useState } from "react";

import { runRequestLike, startRun, type RunSummary } from "./api";
import { runHref } from "../../routes/links";

export function RerunButton({ run }: { run: RunSummary }) {
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const start = () => {
        setBusy(true);
        setError(null);
        startRun(runRequestLike(run)).then(
            (created) => {
                window.location.assign(runHref(created.runId));
            },
            (failure: unknown) => {
                setError(failure instanceof Error ? failure.message : "The run could not be started.");
                setBusy(false);
            },
        );
    };

    return (
        <div className="flex flex-col items-start gap-2.5">
            <Button onClick={start} disabled={busy}>
                {busy ? "Starting…" : "Run again"}
            </Button>
            {error && (
                <Alert variant="destructive">
                    <p className="font-semibold">The run could not be started</p>
                    <p>{error}</p>
                </Alert>
            )}
        </div>
    );
}
