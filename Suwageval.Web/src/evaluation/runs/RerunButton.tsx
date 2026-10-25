import { Alert, Button, Stack } from "@mantine/core";
import { useState } from "react";

import { runRequestLike, startRun, type RunSummary } from "./api";
import { runHref } from "../../routes/links";

/**
 * "Run again": a new run like this one (its agent, tags, endpoint and group), started the way any run is, then its page. Any status may
 * be repeated; the server's refusal is shown as it came.
 */
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
        <Stack gap="xs" align="flex-start">
            <Button onClick={start} loading={busy} disabled={busy}>
                Run again
            </Button>
            {error && (
                <Alert color="red" title="The run could not be started">
                    {error}
                </Alert>
            )}
        </Stack>
    );
}
