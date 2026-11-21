import { Alert, Anchor, Badge, Button, formatDateTime, Label, MultiSelect, Paper, Select, Spinner, Table } from "@maniumek/suwfrontkit";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { lazy, Suspense, useId, useState, type ReactNode } from "react";

import { ARCHIVED_RUNS_PARAM } from "../../archive/archivedParam";
import { ToggleChip } from "../../toggle/ToggleChip";
import { useArchivedParam } from "../../archive/useArchivedParam";
import { ListPagination } from "../../pagination/ListPagination";
import { usePageParam } from "../../pagination/usePageParam";
import { RUNS_PAGE_SIZE } from "../runs/paging";
import { useUnstableChecks } from "../unstable/useUnstableChecks";
import { ChecksCount, SamplesCount } from "../runs/RunCounts";
import { RUN_CHECKS_HELP, RUN_SAMPLES_HELP, runLabel, statusColor, statusLabel } from "../runs/results";
import { agentKeys, agentQuery, tagsQuery, versionsQuery, type AgentDetail, type AgentVersion, type TagUsage } from "../../agents/api";
import { ConfigurationSection } from "./ConfigurationSection";
import { HeaderHelp } from "../../help/HeaderHelp";
import { filterOfValue, filterOptions, filterValue } from "./groupFilter";
import { Page, type PageData } from "../../agents/Page";
import { AreaOff } from "../../agents/AreaOff";
import { areaOn } from "../../agents/features";
import { RunGroupsSection } from "../groups/RunGroupsSection";
import { TagSelect } from "../../tagSelect/TagSelect";
import { useTagFilter } from "../../tagFilter/useTagFilter";
import { useGroupFilter } from "./useGroupFilter";
import { offeredVersions, versionGroupingFor } from "./offeredVersions";
import { useRuns } from "./useRuns";
import {
    allowedVersionIds,
    canCompare,
    consideredVersionIds,
    dataVersionOptions,
    modelsOf,
    runsToCompare,
    versionOptions,
    visibleRuns,
    RUN_CREATED_HELP,
    RUN_FINISHED_HELP,
    RUN_GROUP_HELP,
    RUN_ID_HELP,
    RUN_STATUS_HELP,
    PROBLEMS_SECTION_HELP,
    RUNS_SECTION_HELP,
} from "./view";
import { VERSION_HELP } from "../../versions/view";
import { UnstableChecksCard } from "../unstable/UnstableChecksCard";
import {
    runHref,
    compareGroupHref,
    compareHref,
    versionInUseHref,
    versionsInEvaluationHref,
    COMPARE_MAX_RUNS,
    COMPARE_MIN_RUNS,
} from "../../routes/links";
import { shortId } from "../../format/names";
import { passRatesQuery, type RunPassRates } from "../trend/api";
import { runGroupsQuery, type RunGroupSummary } from "../groups/api";
import { runKeys } from "../runs/api";
import { AgentErrorsCard } from "../errors/AgentErrorsCard";
import { useAgentErrors } from "../errors/useAgentErrors";

const MODEL_HELP =
    "Show only runs of agent versions that use these models (any of the system's LLM agents). The chart, the unstable checks and the agent errors follow the filter.";
const DATA_VERSION_HELP =
    "Show only runs on one of these retrieval data versions: the dataVersion the agent declared when the run started, a fingerprint of what its retrieval can return (content and indexing). New data is not a new agent version: each run keeps the data it ran on. Agents without retrieval have none. The chart, the unstable checks and the agent errors follow the filter.";
const VERSIONS_FILTER_HELP =
    "Show only runs of these agent versions (newest first; empty means all). The chart, the unstable checks and the agent errors follow the filter.";
const GROUP_HELP =
    "Which runs to list by group (run --group): those in no group (the default), those in any group, all runs, or one group. Group runs are experiments, kept out of the regression history and the pass-rate trend. Open a group from the Groups section to compare its runs as variants.";
const ARCHIVED_RUNS_HELP =
    "Runs of archived versions: hidden by default; with this on they are listed and counted in the trend, the unstable checks and the agent errors.";
const TAG_FILTER_PLACEHOLDER = "Every run — type to find a tag";
const TAG_HELP = "The scenario tags the run was limited to (run --tag, repeatable); — means every scenario of the agent ran.";

const Trends = lazy(() => import("../trend/Trends").then((module) => ({ default: module.Trends })));

export function EvaluationPage({ name }: { name: string }) {
    const queryClient = useQueryClient();
    const agent = useQuery(agentQuery(name));
    const versions = useQuery(versionsQuery(name));
    const passRates = useQuery(passRatesQuery(name));
    const groups = useQuery(runGroupsQuery(name));
    const tags = useQuery(tagsQuery(name));
    const parts = [agent, versions, passRates, groups, tags];
    const query: PageData<{
        agent: AgentDetail;
        versions: AgentVersion[];
        passRates: RunPassRates[];
        groups: RunGroupSummary[];
        tags: TagUsage[];
    }> = {
        isPending: parts.some((part) => part.isPending),
        error: parts.find((part) => part.error !== null)?.error ?? null,
        data:
            agent.data && versions.data && passRates.data && groups.data && tags.data
                ? { agent: agent.data, versions: versions.data, passRates: passRates.data, groups: groups.data, tags: tags.data }
                : undefined,
    };

    return (
        <Page query={query} notFound={`There is no agent ${name}.`}>
            {({ agent, versions, passRates, groups, tags }) =>
                !areaOn(agent.features, "evaluation") ? (
                    <AreaOff area="evaluation" />
                ) : (
                    <Suspense fallback={<Spinner label="Loading the charts" />}>
                        <AgentView
                            agent={agent}
                            versions={versions}
                            passRates={passRates}
                            groups={groups}
                            tags={tags}
                            onChanged={() => {
                                void queryClient.invalidateQueries({ queryKey: agentKeys.agent(name) });
                                void queryClient.invalidateQueries({ queryKey: runKeys.all });
                            }}
                        />
                    </Suspense>
                )
            }
        </Page>
    );
}

function AgentView({
    agent,
    versions,
    passRates,
    groups,
    tags,
    onChanged,
}: {
    agent: AgentDetail;
    versions: AgentVersion[];
    passRates: RunPassRates[];
    groups: RunGroupSummary[];
    tags: TagUsage[];
    onChanged: () => void;
}) {
    const [showArchived, setShowArchived] = useArchivedParam(ARCHIVED_RUNS_PARAM);
    const [models, setModels] = useState<string[]>([]);
    const [dataVersions, setDataVersions] = useState<string[]>([]);
    const [versionFilter, setVersionFilter] = useState<string[]>([]);
    const [groupFilter, setGroupFilter] = useGroupFilter();
    const [tagFilter, setTagFilter] = useTagFilter();
    const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
    const versionIds = allowedVersionIds(versions, showArchived, models, dataVersions, versionFilter);
    const [requestedPage, setRequestedPage] = usePageParam("runsPage");
    const { runs, totalCount, page, loading, error } = useRuns(
        agent.name,
        versionIds?.join(",") ?? null,
        groupFilter,
        requestedPage,
        RUNS_PAGE_SIZE,
        tagFilter,
        dataVersions,
    );
    const filtered = <Value,>(change: (value: Value) => void) => {
        return (value: Value) => {
            change(value);
            setRequestedPage(1);
        };
    };

    const offered = offeredVersions(versions, versionGroupingFor(groupFilter), models, dataVersions, versionFilter);
    const toCompare = runsToCompare(runs, selected);

    const considered = consideredVersionIds(versions, showArchived, models, dataVersions, versionFilter);
    const problemsAgent = considered.length === 0 ? null : agent.name;
    const { checks: unstable, arrived: checksArrived } = useUnstableChecks(problemsAgent, considered, false, dataVersions);
    const { errors, arrived: errorsArrived } = useAgentErrors(problemsAgent, considered, dataVersions);
    const trendPoints = visibleRuns(passRates, versions, showArchived, models, dataVersions, versionFilter);

    if (!checksArrived || !errorsArrived) return <Spinner label="Loading the problems" />;
    return (
        <div className="flex flex-col gap-5">
            <Anchor href="/">← Agents</Anchor>
            <div className="agent-layout">
                <div className="agent-layout-header">
                    <Paper className="flex flex-col gap-1">
                        <div className="flex flex-wrap items-center justify-between gap-4">
                            <h2 className="text-[26px] leading-tight font-bold">{agent.name}</h2>
                            <Anchor href={versionsInEvaluationHref(agent.name)}>Versions in evaluation</Anchor>
                        </div>
                        <p className="text-muted-foreground">
                            {agent.endpointUrl} · created {formatDateTime(agent.createdAt)}
                        </p>
                        <p className="text-muted-foreground">
                            versions {agent.versions} · scenarios {agent.scenarios} · checks {agent.checks}
                        </p>
                    </Paper>
                </div>

                <div className="agent-layout-analytics">
                    <div className="flex flex-col gap-5">
                        {(unstable.length > 0 || errors.length > 0) && (
                            <div className="flex flex-col gap-3">
                                <h3 className="text-[22px] font-bold">
                                    <HeaderHelp label="Problems" help={PROBLEMS_SECTION_HELP} />
                                </h3>
                                <div className="agent-health-cards">
                                    <UnstableChecksCard checks={unstable} />
                                    <AgentErrorsCard errors={errors} />
                                </div>
                            </div>
                        )}
                        <Trends points={trendPoints} />
                    </div>
                </div>

                <div className="agent-layout-main flex flex-col gap-5">
                    <RunGroupsSection agentName={agent.name} groups={groups} onChanged={onChanged} />
                    <h3 className="text-[22px] font-bold">
                        <HeaderHelp label="Runs" help={RUNS_SECTION_HELP} />
                    </h3>
                    <div className="flex flex-wrap items-end justify-between gap-4">
                        <div className="flex flex-wrap items-end gap-4">
                            <Filter label={<HeaderHelp label="Model" help={MODEL_HELP} />}>
                                {(id) => (
                                    <MultiSelect
                                        id={id}
                                        label="Model"
                                        placeholder="All models"
                                        options={modelsOf(offered).map((model) => ({ value: model, label: model }))}
                                        value={models}
                                        onValueChange={filtered(setModels)}
                                    />
                                )}
                            </Filter>
                            <Filter label={<HeaderHelp label="Data version" help={DATA_VERSION_HELP} />}>
                                {(id) => (
                                    <MultiSelect
                                        id={id}
                                        label="Data version"
                                        placeholder="All data versions"
                                        options={dataVersionOptions(offered)}
                                        value={dataVersions}
                                        onValueChange={filtered(setDataVersions)}
                                    />
                                )}
                            </Filter>
                            <Filter label={<HeaderHelp label="Versions" help={VERSIONS_FILTER_HELP} />}>
                                {(id) => (
                                    <MultiSelect
                                        id={id}
                                        label="Versions"
                                        placeholder="All versions"
                                        options={versionOptions(offered)}
                                        value={versionFilter}
                                        onValueChange={filtered(setVersionFilter)}
                                    />
                                )}
                            </Filter>
                            <Filter label={<HeaderHelp label="Group" help={GROUP_HELP} />}>
                                {(id) => (
                                    <Select
                                        id={id}
                                        aria-label="Group"
                                        className="w-full"
                                        value={filterValue(groupFilter)}
                                        onChange={(event) => {
                                            filtered(setGroupFilter)(filterOfValue(event.currentTarget.value));
                                        }}
                                    >
                                        {filterOptions(groups, groupFilter).map((option) => (
                                            <option key={option.value} value={option.value}>
                                                {option.label}
                                            </option>
                                        ))}
                                    </Select>
                                )}
                            </Filter>
                            <TagSelect
                                label="Filter runs by tag"
                                placeholder={TAG_FILTER_PLACEHOLDER}
                                tags={tags.map((usage) => usage.tag)}
                                selected={tagFilter}
                                onChange={filtered(setTagFilter)}
                            />
                            <ToggleChip
                                label="Archived"
                                checked={showArchived}
                                help={ARCHIVED_RUNS_HELP}
                                onChange={filtered(setShowArchived)}
                            />
                        </div>
                        <Button
                            disabled={!canCompare(toCompare.length)}
                            title={`Select ${COMPARE_MIN_RUNS} to ${COMPARE_MAX_RUNS} runs to compare`}
                            onClick={() => {
                                window.location.assign(compareHref(toCompare));
                            }}
                        >
                            Compare ({toCompare.length})
                        </Button>
                    </div>
                    {error !== null && (
                        <Alert variant="destructive">
                            <p className="font-semibold">Could not load the runs</p>
                            <p>{error}</p>
                        </Alert>
                    )}
                    {runs.length === 0 ? (
                        loading ? (
                            <Spinner label="Loading runs" />
                        ) : (
                            error === null && (
                                <p className="text-muted-foreground">
                                    {versionIds !== null
                                        ? "No run matches the filters — archived versions are hidden unless the Archived chip is on."
                                        : groupFilter.kind === "none" && groups.length > 0
                                          ? "This agent has no runs outside groups yet — runs started in a group are listed under Any group or All runs."
                                          : "This agent has no runs yet."}
                                </p>
                            )
                        )
                    ) : (
                        <Paper className="flex flex-col gap-4">
                            <Table className="min-w-[1100px]">
                                <thead>
                                    <tr>
                                        <th />
                                        <th className="no-wrap">
                                            <HeaderHelp label="Run" help={RUN_ID_HELP} />
                                        </th>
                                        <th className="no-wrap">
                                            <HeaderHelp label="Status" help={RUN_STATUS_HELP} />
                                        </th>
                                        <th>
                                            <HeaderHelp label="Tags" help={TAG_HELP} />
                                        </th>
                                        <th>
                                            <HeaderHelp label="Group" help={RUN_GROUP_HELP} />
                                        </th>
                                        <th>
                                            <HeaderHelp label="Version" help={VERSION_HELP} />
                                        </th>
                                        <th className="no-wrap">
                                            <HeaderHelp label="Checks" help={RUN_CHECKS_HELP} />
                                        </th>
                                        <th className="no-wrap">
                                            <HeaderHelp label="Samples" help={RUN_SAMPLES_HELP} />
                                        </th>
                                        <th className="no-wrap">
                                            <HeaderHelp label="Created" help={RUN_CREATED_HELP} />
                                        </th>
                                        <th className="no-wrap">
                                            <HeaderHelp label="Finished" help={RUN_FINISHED_HELP} />
                                        </th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {runs.map((run) => (
                                        <tr key={run.id}>
                                            <td>
                                                <input
                                                    type="checkbox"
                                                    className="size-4 accent-primary"
                                                    aria-label={`Select run ${shortId(run.id)}`}
                                                    checked={selected.has(run.id)}
                                                    onChange={(event) => {
                                                        const next = new Set(selected);
                                                        if (event.currentTarget.checked) next.add(run.id);
                                                        else next.delete(run.id);
                                                        setSelected(next);
                                                    }}
                                                />
                                            </td>
                                            <td className="no-wrap">
                                                <Anchor href={runHref(run.id)}>{shortId(run.id)}</Anchor>
                                            </td>
                                            <td className="no-wrap">
                                                <div className="flex flex-col items-start gap-1">
                                                    <Badge variant={statusColor(run.status)}>{statusLabel(run.status)}</Badge>
                                                    {run.reference === true && <Badge variant="success">Reference</Badge>}
                                                    {run.recheckOf != null && <Badge>Re-check</Badge>}
                                                </div>
                                            </td>
                                            <td>{run.tags?.length ? run.tags.join(", ") : "—"}</td>
                                            <td className="wrap-key">
                                                {run.group == null ? (
                                                    "—"
                                                ) : (
                                                    <Anchor href={compareGroupHref(run.group, agent.name)}>{run.group}</Anchor>
                                                )}
                                            </td>
                                            <td>
                                                <Anchor href={versionInUseHref(agent.name, run.agentVersionId, run.group != null)}>
                                                    {runLabel(run, versions.find((version) => version.id === run.agentVersionId)?.label)}
                                                </Anchor>
                                            </td>
                                            <td className="no-wrap">
                                                <ChecksCount run={run} />
                                            </td>
                                            <td className="no-wrap">
                                                <SamplesCount run={run} />
                                            </td>
                                            <td className="no-wrap">{formatDateTime(run.createdAt)}</td>
                                            <td className="no-wrap">{formatDateTime(run.finishedAt)}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </Table>
                            <ListPagination
                                totalCount={totalCount}
                                page={page}
                                pageSize={RUNS_PAGE_SIZE}
                                onChange={(next) => {
                                    setRequestedPage(next);
                                    setSelected(new Set());
                                }}
                            />
                        </Paper>
                    )}

                    <ConfigurationSection endpointUrl={agent.endpointUrl} samples={agent.samples} regression={agent.regression} />
                </div>
            </div>
        </div>
    );
}

function Filter({ label, children }: { label: ReactNode; children: (id: string) => ReactNode }) {
    const id = useId();
    return (
        <div className="flex w-[260px] flex-col gap-1">
            <Label htmlFor={id}>{label}</Label>
            {children(id)}
        </div>
    );
}
