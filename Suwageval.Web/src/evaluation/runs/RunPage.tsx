import { DataVersion } from "../../dataVersion/DataVersion";
import { Alert, Anchor, Badge, Box, Group, List, Loader, Paper, Stack, Text, Title } from "@mantine/core";

import { isNotFound } from "../../api/client";
import { CancelButton } from "./CancelButton";
import { HeaderHelp } from "../../help/HeaderHelp";
import { ChecksCount, SamplesCount } from "./RunCounts";
import { RunNote } from "./note/RunNote";
import { RecheckButton } from "./RecheckButton";
import { RegressionSection } from "./RegressionSection";
import { RerunButton } from "./RerunButton";
import { ScenarioCard } from "./ScenarioCard";
import {
    durationSummary,
    isActive,
    RUN_CHECKS_HELP,
    RUN_SAMPLES_HELP,
    runStatusLabel,
    scenariosChecksCounts,
    scenariosSamplesCounts,
    statusColor,
} from "./results";
import { filteredLabel, keepsTags } from "../../tagFilter/tagSelection";
import { TagFilter } from "../../tagFilter/TagFilter";
import { useTagFilter } from "../../tagFilter/useTagFilter";
import { hiddenCounts, hiddenLine, visibleScenarios } from "./hidePassed";
import { useHidePassed } from "./useHidePassed";
import { useRunResults } from "./useRunResults";
import { useScrollToHash } from "./useScrollToHash";
import { useUnstableChecks } from "../unstable/useUnstableChecks";
import { agentEvaluationHref, runHref, compareGroupHref, versionInUseHref } from "../../routes/links";
import { formatTime } from "../../format/time";
import { PrintButton } from "../../print/PrintButton";
import { usePrintOnLoad } from "../../print/usePrintOnLoad";
import { runPrintLine } from "./printLine";
import { useReportAgent } from "../../shell/reportAgent";
import { ToggleChip } from "../../toggle/ToggleChip";

/** One run's results at `/runs/<id>`: the header, then a card per scenario. */
export function RunPage({ runId }: { runId: string }) {
    const state = useRunResults(runId);
    const [selectedTags, selectTags] = useTagFilter();
    const [hidePassed, setHidePassed] = useHidePassed();
    // Asked once the run is known: the agent's unstable checks of this run's version.
    const loadedRun = state.kind === "loaded" ? state.results.run : null;
    const unstable = useUnstableChecks(loadedRun?.agent ?? null, loadedRun === null ? [] : [loadedRun.agentVersionId]);
    useReportAgent(loadedRun?.agent ?? null);
    useScrollToHash(state.kind === "loaded");
    usePrintOnLoad(state.kind === "loaded");

    if (state.kind === "loading") return <Loader aria-label="Loading results" />;
    if (state.kind === "error") {
        const notFound = isNotFound(state.error);
        return (
            <Alert color="red" title={notFound ? "Run not found" : "Could not load the results"}>
                {notFound ? `There is no run ${runId}.` : state.error.message}
            </Alert>
        );
    }

    const { run } = state.results;
    const filtered = selectedTags.length > 0;
    const scenarios = state.results.scenarios.filter((scenario) => keepsTags(scenario.tags, selectedTags));
    const checks = filtered ? scenariosChecksCounts(scenarios) : run;
    const samples = filtered ? scenariosSamplesCounts(scenarios) : run;
    const turnTime = durationSummary(state.results, filtered ? "Turn time (all scenarios)" : "Turn time");
    return (
        <>
            <Stack gap="md">
                <Group gap="xs" className="no-print">
                    <Anchor href="/">Agents</Anchor>/<Anchor href={agentEvaluationHref(run.agent)}>{run.agent}</Anchor>
                </Group>
                <Group gap="sm">
                    <Title order={2}>Run {run.agent}</Title>
                    <Badge size="lg" color={statusColor(run.status)}>
                        {runStatusLabel(run)}
                    </Badge>
                    {isActive(run.status) && <Loader size="sm" aria-label="Run in progress" />}
                </Group>
                <Text c="dimmed">
                    id {run.id} · version{" "}
                    <Anchor href={versionInUseHref(run.agent, run.agentVersionId, run.group != null)}>{run.agentVersionId}</Anchor>
                    {run.label?.trim() ? ` · label ${run.label}` : ""}
                    {run.tags?.length ? ` · tags ${run.tags.join(", ")}` : ""}
                    {run.group != null && (
                        <>
                            {" · group "}
                            <Anchor href={compareGroupHref(run.group, run.agent)}>{run.group}</Anchor>
                        </>
                    )}
                    {" · "}
                    {run.endpointUrl}
                </Text>
                {run.dataVersion != null && <DataVersion size="sm" dataVersion={run.dataVersion} description={run.dataDescription} />}
                <RunNote key={run.id} runId={run.id} initial={run.note} />
                {run.group != null && (
                    <Text c="dimmed" size="sm">
                        A run in a group is an experiment: it has no regression states, is never a reference run and is not in the agent's
                        pass-rate trend.
                    </Text>
                )}
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
                <Group gap="sm" align="flex-start" className="no-print">
                    <RerunButton runId={run.id} />
                    <RecheckButton run={run} />
                    <CancelButton run={run} />
                </Group>
                {/* The filters stay applied on paper; the print-only line says which. */}
                <Text className="print-only print-line">{runPrintLine(run, selectedTags, hidePassed)}</Text>
                <Box className="no-print">
                    <TagFilter
                        tags={state.results.scenarios.flatMap((scenario) => scenario.tags)}
                        selected={selectedTags}
                        onChange={selectTags}
                    />
                </Box>
                <Group gap="lg" className="no-print">
                    <ToggleChip label="Hide passed checks" checked={hidePassed} onChange={setHidePassed} />
                    <PrintButton />
                </Group>
                {hidePassed && (
                    <Text size="sm" className="no-print">
                        {hiddenLine(hiddenCounts(scenarios))}
                    </Text>
                )}
                <Paper withBorder p="md">
                    <Group gap="lg">
                        <Text>
                            <HeaderHelp label={filteredLabel("Samples", selectedTags)} help={RUN_SAMPLES_HELP} />:{" "}
                            <SamplesCount run={samples} />
                        </Text>
                        <Text>
                            <HeaderHelp label={filteredLabel("Checks", selectedTags)} help={RUN_CHECKS_HELP} />:{" "}
                            <ChecksCount run={checks} />
                        </Text>
                    </Group>
                    {turnTime !== null && <Text>{turnTime}</Text>}
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
                {scenarios.length === 0 && <Text c="dimmed">No scenario has any of the selected tags.</Text>}
                {visibleScenarios(scenarios, hidePassed).map((scenario) => (
                    <ScenarioCard
                        key={scenario.key}
                        scenario={scenario}
                        versionId={run.agentVersionId}
                        unstable={unstable}
                        hidePassed={hidePassed}
                    />
                ))}
            </Stack>
        </>
    );
}
