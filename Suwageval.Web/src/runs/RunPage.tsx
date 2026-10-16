import { Alert, Anchor, Badge, Container, Group, Loader, Stack, Text, Title } from "@mantine/core";

import { ScenarioCard } from "./ScenarioCard";
import { formatTime, isActive, isRunNotFound, num, progress, statusColor, statusLabel } from "./results";
import { useRunResults } from "./useRunResults";

/** One run's results at `/runs/<id>`: the header, then a card per scenario. */
export function RunPage({ runId }: { runId: string }) {
    const state = useRunResults(runId);

    if (state.kind === "loading") {
        return (
            <Container py="xl">
                <Loader aria-label="Loading results" />
            </Container>
        );
    }
    if (state.kind === "error") {
        const notFound = isRunNotFound(state.error);
        return (
            <Container py="xl">
                <Alert color="red" title={notFound ? "Run not found" : "Could not load the results"}>
                    {notFound ? `There is no run ${runId}.` : state.error.message}
                </Alert>
            </Container>
        );
    }

    const { run, scenarios } = state.results;
    return (
        <Container py="xl" size="lg">
            <Stack gap="md">
                <Group gap="xs">
                    <Anchor href="/">Agents</Anchor>/<Anchor href={`/agents/${encodeURIComponent(run.agent)}`}>{run.agent}</Anchor>
                </Group>
                <Group gap="sm">
                    <Title order={2}>Run {run.agent}</Title>
                    <Badge size="lg" color={statusColor(run.status)}>
                        {statusLabel(run.status)}
                    </Badge>
                    {isActive(run.status) && <Loader size="sm" aria-label="Run in progress" />}
                </Group>
                <Text c="dimmed">
                    id {run.id} · version {run.agentVersionId}
                    {run.tag ? ` · tag ${run.tag}` : ""} · {run.endpointUrl}
                </Text>
                <Group gap="lg">
                    <Text>Samples: {progress(run)}</Text>
                    <Text c="green">Checks passed: {num(run.checksPassed)}</Text>
                    <Text c={num(run.checksFailed) > 0 ? "red" : "dimmed"}>Checks failed: {num(run.checksFailed)}</Text>
                </Group>
                <Text c="dimmed" size="sm">
                    created {formatTime(run.createdAt)} · start {formatTime(run.startedAt)} · koniec {formatTime(run.finishedAt)}
                </Text>
                {run.contractChangedDuringRun === true && (
                    <Alert color="yellow" title="The agent's contract changed during the run">
                        Some samples may not come from this version.
                    </Alert>
                )}
                {run.error && (
                    <Alert color="red" title="The run failed">
                        {run.error}
                    </Alert>
                )}
                {scenarios.map((scenario) => (
                    <ScenarioCard key={scenario.key} scenario={scenario} />
                ))}
            </Stack>
        </Container>
    );
}
