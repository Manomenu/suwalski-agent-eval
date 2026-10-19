import { Alert, Anchor, Group, Loader, Stack, Text, Title } from "@mantine/core";
import { useState } from "react";

import { fetchVersions } from "../agents/api";
import { useLoaded } from "../agents/useLoaded";
import { agentHref } from "../agents/view";
import { fetchComparison, type RunComparison } from "./api";
import { baselineFirst, parseRunIds } from "./compare";
import { CompareTable } from "./CompareTable";

/** `/compare?runs=<id>,<id>,…`: 2 to 5 runs of one agent side by side. `runs` is the query as it came; this checks it. */
export function ComparePage({ runs }: { runs: string }) {
    const parsed = parseRunIds(runs);
    // A wrong query asks for nothing: no request is made.
    return parsed.kind === "error" ? <Problem message={parsed.message} /> : <Comparison ids={parsed.ids} />;
}

function Comparison({ ids }: { ids: string[] }) {
    // The server reads the first run as the baseline: another baseline is the same runs asked for again, that run first.
    const [baselineId, setBaselineId] = useState(ids[0] ?? "");
    const ordered = baselineFirst(ids, baselineId);
    const state = useLoaded(ordered.join(","), (signal) => fetchComparison(ordered, signal));

    if (state.kind === "loading") return <Loader aria-label="Loading the comparison" />;
    if (state.kind === "error") return <Problem message={state.error.message} />;
    return <Loaded comparison={state.value} onBaseline={setBaselineId} />;
}

function Loaded({ comparison, onBaseline }: { comparison: RunComparison; onBaseline: (runId: string) => void }) {
    const agent = comparison.runs[0]?.run.agent ?? "";
    const versions = useLoaded(agent, (signal) => fetchVersions(agent, signal));

    if (versions.kind === "loading") return <Loader aria-label="Loading the comparison" />;
    if (versions.kind === "error") return <Problem message={versions.error.message} />;
    return (
        <Stack gap="md">
            <Group gap="xs">
                <Anchor href="/">Agents</Anchor>/<Anchor href={agentHref(agent)}>{agent}</Anchor>
            </Group>
            <Title order={2}>Compare runs of {agent}</Title>
            <Text c="dimmed">
                Every run is read against the baseline: the first run, until you pick another by its header. A check passes when its pass
                rate reaches its threshold.
            </Text>
            <CompareTable comparison={comparison} versions={versions.value} onBaseline={onBaseline} />
        </Stack>
    );
}

function Problem({ message }: { message: string }) {
    return (
        <Stack gap="sm">
            <Alert color="red" title="Cannot compare">
                {message}
            </Alert>
            <Anchor href="/">Back to the list of agents</Anchor>
        </Stack>
    );
}
