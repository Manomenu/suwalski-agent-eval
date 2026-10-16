import { Alert, Anchor, Group, Loader, Stack, Text, Title } from "@mantine/core";

import { useLoaded } from "../agents/useLoaded";
import { agentHref } from "../agents/view";
import { fetchComparison } from "./api";
import { parseRunIds } from "./compare";
import { CompareTable } from "./CompareTable";

/** `/compare?runs=<id>,<id>,…`: 2 to 5 runs of one agent side by side. `runs` is the query as it came; this checks it. */
export function ComparePage({ runs }: { runs: string }) {
    const parsed = parseRunIds(runs);
    // A wrong query asks for nothing: no request is made.
    return parsed.kind === "error" ? <Problem message={parsed.message} /> : <Comparison ids={parsed.ids} />;
}

function Comparison({ ids }: { ids: string[] }) {
    const state = useLoaded(ids.join(","), (signal) => fetchComparison(ids, signal));

    if (state.kind === "loading") return <Loader aria-label="Loading the comparison" />;
    if (state.kind === "error") return <Problem message={state.error.message} />;

    const agent = state.value.runs[0]?.run.agent ?? "";
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
            <CompareTable comparison={state.value} firstBaseline={ids[0] ?? ""} />
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
