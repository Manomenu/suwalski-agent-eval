import { Alert, Anchor, Badge, Group, List, Loader, Paper, Stack, Text, Title } from "@mantine/core";

import { isNotFound } from "../api/client";
import { CancelButton } from "./CancelButton";
import { HeaderHelp } from "./HeaderHelp";
import { ChecksCount, SamplesCount } from "./RunCounts";
import { RecheckButton } from "./RecheckButton";
import { RegressionSection } from "./RegressionSection";
import { RerunButton } from "./RerunButton";
import { ScenarioCard } from "./ScenarioCard";
import {
    durationSummary,
    formatTime,
    isActive,
    RUN_CHECKS_HELP,
    RUN_SAMPLES_HELP,
    runHref,
    runStatusLabel,
    statusColor,
    variantsGroupHref,
} from "./results";
import { useRunResults } from "./useRunResults";
import { useUnstableChecks } from "./useUnstableChecks";

/** One run's results at `/runs/<id>`: the header, then a card per scenario. */
export function RunPage({ runId }: { runId: string }) {
    const state = useRunResults(runId);
    // Asked once the run is known: the agent's unstable checks of this run's version.
    const loadedRun = state.kind === "loaded" ? state.results.run : null;
    const unstable = useUnstableChecks(loadedRun?.agent ?? null, loadedRun === null ? [] : [loadedRun.agentVersionId]);

    if (state.kind === "loading") return <Loader aria-label="Loading results" />;
    if (state.kind === "error") {
        const notFound = isNotFound(state.error);
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
                    {run.tags?.length ? ` · tags ${run.tags.join(", ")}` : ""}
                    {run.group != null && (
                        <>
                            {" · group "}
                            <Anchor href={variantsGroupHref(run.group, run.agent)}>{run.group}</Anchor>
                        </>
                    )}
                    {" · "}
                    {run.endpointUrl}
                </Text>
                {run.recheckOf != null && (
                    <Alert color="blue" title="Re-check">
                        <Text>
                            Re-check of <Anchor href={runHref(run.recheckOf)}>run {run.recheckOf}</Anchor>: its stored answers judged again
                            by the current checks, without calling the agent. The turn times are the source's, copied, not measured again.
                        </Text>
                        {(run.skippedScenarios?.length ?? 0) > 0 && (
                            <>
                                <Text fw={600} mt="xs">
                                    Skipped scenarios
                                </Text>
                                <List size="sm">
                                    {run.skippedScenarios?.map((skipped) => (
                                        <List.Item key={skipped.key} className="wrap-key">
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
                        <Text>
                            <HeaderHelp label="Samples" help={RUN_SAMPLES_HELP} />: <SamplesCount run={run} />
                        </Text>
                        <Text>
                            <HeaderHelp label="Checks" help={RUN_CHECKS_HELP} />: <ChecksCount run={run} />
                        </Text>
                    </Group>
                    {durationSummary(state.results) && <Text>{durationSummary(state.results)}</Text>}
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
                <RegressionSection results={state.results} />
                {scenarios.map((scenario) => (
                    <ScenarioCard key={scenario.key} scenario={scenario} versionId={run.agentVersionId} unstable={unstable} />
                ))}
            </Stack>
        </>
    );
}
