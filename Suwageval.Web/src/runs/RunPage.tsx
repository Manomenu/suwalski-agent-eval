import { Alert, Anchor, Badge, Group, List, Loader, Paper, Stack, Text, Title } from "@mantine/core";

import { CancelButton } from "./CancelButton";
import { RecheckButton } from "./RecheckButton";
import { RerunButton } from "./RerunButton";
import { recheckSource } from "./recheck";
import { ScenarioCard } from "./ScenarioCard";
import { durationSummary, formatTime, isActive, isRunNotFound, num, progress, runHref, runStatusLabel, statusColor } from "./results";
import { useRunResults } from "./useRunResults";

/** One run's results at `/runs/<id>`: the header, then a card per scenario. */
export function RunPage({ runId }: { runId: string }) {
    const state = useRunResults(runId);

    if (state.kind === "loading") return <Loader aria-label="Loading results" />;
    if (state.kind === "error") {
        const notFound = isRunNotFound(state.error);
        return (
            <Alert color="red" title={notFound ? "Run not found" : "Could not load the results"}>
                {notFound ? `There is no run ${runId}.` : state.error.message}
            </Alert>
        );
    }

    const { run, scenarios } = state.results;
    return (
        <>
            <Stack gap="md">
                <Group gap="xs">
                    <Anchor href="/">Agents</Anchor>/<Anchor href={`/agents/${encodeURIComponent(run.agent)}`}>{run.agent}</Anchor>
                </Group>
                <Group gap="sm">
                    <Title order={2}>Run {run.agent}</Title>
                    <Badge size="lg" color={statusColor(run.status)}>
                        {runStatusLabel(run)}
                    </Badge>
                    {isActive(run.status) && <Loader size="sm" aria-label="Run in progress" />}
                </Group>
                <Text c="dimmed">
                    id {run.id} · version {run.agentVersionId}
                    {run.tag ? ` · tag ${run.tag}` : ""} · {run.endpointUrl}
                </Text>
                {recheckSource(run) !== null && (
                    <Alert color="blue" title="Re-check">
                        <Text>
                            Re-check of <Anchor href={runHref(recheckSource(run) ?? "")}>run {recheckSource(run)}</Anchor>: its stored
                            answers judged again by the current checks, without calling the agent. The turn times are the source's, copied,
                            not measured again.
                        </Text>
                        {(run.skippedScenarios?.length ?? 0) > 0 && (
                            <>
                                <Text fw={600} mt="xs">
                                    Skipped scenarios
                                </Text>
                                <List size="sm">
                                    {run.skippedScenarios?.map((skipped) => (
                                        <List.Item key={skipped.key}>
                                            {skipped.key}: {skipped.reason}
                                        </List.Item>
                                    ))}
                                </List>
                            </>
                        )}
                    </Alert>
                )}
                <Group gap="sm" align="flex-start">
                    <RerunButton runId={run.id} />
                    <RecheckButton run={run} />
                    <CancelButton run={run} />
                </Group>
                <Paper withBorder p="md">
                    <Group gap="lg">
                        <Text>Samples: {progress(run)}</Text>
                        <Text c="green">Checks passed: {num(run.checksPassed)}</Text>
                        <Text c={num(run.checksFailed) > 0 ? "red" : "dimmed"}>Checks failed: {num(run.checksFailed)}</Text>
                    </Group>
                    {durationSummary(scenarios) && <Text>{durationSummary(scenarios)}</Text>}
                    <Text c="dimmed" size="sm">
                        created {formatTime(run.createdAt)} · start {formatTime(run.startedAt)} · finished {formatTime(run.finishedAt)}
                    </Text>
                </Paper>
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
        </>
    );
}
