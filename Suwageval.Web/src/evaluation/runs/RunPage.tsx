import { Alert, Anchor, Badge, formatDateTime, Paper, Spinner, ToggleChip } from "@maniumek/suwfrontkit";
import { DataVersion } from "../../dataVersion/DataVersion";

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
import { PrintButton } from "../../print/PrintButton";
import { usePrintOnLoad } from "../../print/usePrintOnLoad";
import { runPrintLine } from "./printLine";
import { useReportAgent } from "../../shell/reportAgent";

export function RunPage({ runId }: { runId: string }) {
    const state = useRunResults(runId);
    const [selectedTags, selectTags] = useTagFilter();
    const [hidePassed, setHidePassed] = useHidePassed();
    const loadedRun = state.kind === "loaded" ? state.results.run : null;
    const { checks: unstable } = useUnstableChecks(loadedRun?.agent ?? null, loadedRun === null ? [] : [loadedRun.agentVersionId]);
    useReportAgent(loadedRun?.agent ?? null);
    useScrollToHash(state.kind === "loaded");
    usePrintOnLoad(state.kind === "loaded");

    if (state.kind === "loading") return <Spinner label="Loading results" />;
    if (state.kind === "error") {
        const notFound = isNotFound(state.error);
        return (
            <Alert variant="destructive">
                <p className="font-semibold">{notFound ? "Run not found" : "Could not load the results"}</p>
                <p>{notFound ? `There is no run ${runId}.` : state.error.message}</p>
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
            <div className="flex flex-col gap-4">
                <div className="no-print flex flex-wrap items-center gap-2.5">
                    <Anchor href="/">Agents</Anchor>/<Anchor href={agentEvaluationHref(run.agent)}>{run.agent}</Anchor>
                </div>
                <div className="flex flex-wrap items-center gap-3">
                    <h2 className="text-[26px] leading-tight font-bold">Run {run.agent}</h2>
                    <Badge variant={statusColor(run.status)}>{runStatusLabel(run)}</Badge>
                    {isActive(run.status) && <Spinner size="sm" className="text-sm" label="Updating as results arrive" />}
                </div>
                <p className="text-muted-foreground">
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
                </p>
                {run.dataVersion != null && <DataVersion size="sm" dataVersion={run.dataVersion} description={run.dataDescription} />}
                <RunNote key={run.id} runId={run.id} initial={run.note} />
                {run.group != null && (
                    <p className="text-sm text-muted-foreground">
                        A run in a group is an experiment: it has no regression states, is never a reference run and is not in the agent's
                        pass-rate trend.
                    </p>
                )}
                {run.recheckOf != null && (
                    <Alert>
                        <p className="font-semibold">Re-check</p>
                        <p>
                            Re-check of <Anchor href={runHref(run.recheckOf)}>run {run.recheckOf}</Anchor>: its stored answers judged again
                            by the current checks, without calling the agent. The turn times are the source's, copied, not measured again.
                        </p>
                        {(run.skippedScenarios?.length ?? 0) > 0 && (
                            <>
                                <p className="mt-2.5 font-semibold">Skipped scenarios</p>
                                <ul className="list-disc pl-5 text-sm">
                                    {run.skippedScenarios?.map((skipped) => (
                                        <li key={skipped.key} className="wrap-key">
                                            {skipped.key}: {skipped.reason}
                                        </li>
                                    ))}
                                </ul>
                            </>
                        )}
                    </Alert>
                )}
                <div className="no-print flex flex-wrap items-start gap-3">
                    <RerunButton run={run} />
                    <RecheckButton run={run} />
                    <CancelButton run={run} />
                </div>
                <p className="print-only print-line">{runPrintLine(run, selectedTags, hidePassed)}</p>
                <div className="no-print">
                    <TagFilter
                        tags={state.results.scenarios.flatMap((scenario) => scenario.tags)}
                        selected={selectedTags}
                        onChange={selectTags}
                    />
                </div>
                <div className="no-print flex flex-wrap items-center gap-5">
                    <ToggleChip pressed={hidePassed} onPressedChange={setHidePassed}>
                        Hide passed checks
                    </ToggleChip>
                    <PrintButton />
                </div>
                {hidePassed && <p className="no-print text-sm">{hiddenLine(hiddenCounts(scenarios))}</p>}
                <Paper>
                    <div className="flex flex-wrap items-center gap-5">
                        <p>
                            <HeaderHelp label={filteredLabel("Samples", selectedTags)} help={RUN_SAMPLES_HELP} />:{" "}
                            <SamplesCount run={samples} />
                        </p>
                        <p>
                            <HeaderHelp label={filteredLabel("Checks", selectedTags)} help={RUN_CHECKS_HELP} />:{" "}
                            <ChecksCount run={checks} />
                        </p>
                    </div>
                    {turnTime !== null && <p>{turnTime}</p>}
                    <p className="text-sm text-muted-foreground">
                        created {formatDateTime(run.createdAt)} · start {formatDateTime(run.startedAt)} · finished{" "}
                        {formatDateTime(run.finishedAt)}
                    </p>
                </Paper>
                {run.contractChangedDuringRun === true && (
                    <Alert variant="warning">
                        <p className="font-semibold">The agent's contract changed during the run</p>
                        Some samples may not come from this version.
                    </Alert>
                )}
                {run.error && (
                    <Alert variant="destructive">
                        <p className="font-semibold">The run failed</p>
                        <p>{run.error}</p>
                    </Alert>
                )}
                <RegressionSection results={state.results} />
                {scenarios.length === 0 && <p className="text-muted-foreground">No scenario has any of the selected tags.</p>}
                {visibleScenarios(scenarios, hidePassed).map((scenario) => (
                    <ScenarioCard
                        key={scenario.key}
                        scenario={scenario}
                        versionId={run.agentVersionId}
                        unstable={unstable}
                        hidePassed={hidePassed}
                    />
                ))}
            </div>
        </>
    );
}
