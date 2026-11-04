import {
    Alert,
    Anchor,
    Badge,
    Button,
    Checkbox,
    Group,
    Loader,
    MultiSelect,
    Paper,
    Select,
    Stack,
    Table,
    Text,
    Title,
} from "@mantine/core";
import { lazy, Suspense, useState } from "react";

import { ARCHIVED_RUNS_PARAM } from "../../archive/archivedParam";
import { ToggleChip } from "../../toggle/ToggleChip";
import { useArchivedParam } from "../../archive/useArchivedParam";
import { ListPagination } from "../../pagination/ListPagination";
import { usePageParam } from "../../pagination/usePageParam";
import { RUNS_PAGE_SIZE } from "../runs/paging";
import { useUnstableChecks } from "../unstable/useUnstableChecks";
import { ChecksCount, SamplesCount } from "../runs/RunCounts";
import { RUN_CHECKS_HELP, RUN_SAMPLES_HELP, runLabel, statusColor, statusLabel } from "../runs/results";
import { fetchAgent, fetchTags, fetchVersions, type AgentVersion, type TagUsage } from "../../agents/api";
import { ConfigurationSection } from "./ConfigurationSection";
import { HeaderHelp } from "../../help/HeaderHelp";
import { filterOfValue, filterOptions, filterValue } from "./groupFilter";
import { Page } from "../../agents/Page";
import { AreaOff } from "../../agents/AreaOff";
import { areaOn } from "../../agents/features";
import { RunGroupsSection } from "../groups/RunGroupsSection";
import { TagSelect } from "../../tagSelect/TagSelect";
import { useTagFilter } from "../../tagFilter/useTagFilter";
import { useGroupFilter } from "./useGroupFilter";
import { offeredVersions, versionGroupingFor } from "./offeredVersions";
import { useRequest } from "../../api/useRequest";
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
import { formatTime } from "../../format/time";
import { shortId } from "../../format/names";
import { fetchPassRates } from "../trend/api";
import { fetchRunGroups, type RunGroupSummary } from "../groups/api";
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

const MODEL_FILTER_MIN_WIDTH = 260;
const RUNS_TABLE_MIN_WIDTH = 1100;

export function EvaluationPage({ name }: { name: string }) {
    const [reload, setReload] = useState(0);
    const state = useRequest(`${name}/${reload}`, async (signal) => {
        const [agent, versions, passRates, groups, tags] = await Promise.all([
            fetchAgent(name, signal),
            fetchVersions(name, signal),
            fetchPassRates(name, signal),
            fetchRunGroups(name, signal),
            fetchTags(name, signal),
        ]);
        return { agent, versions, passRates, groups, tags };
    });

    return (
        <Page state={state} notFound={`There is no agent ${name}.`}>
            {({ agent, versions, passRates, groups, tags }) =>
                !areaOn(agent.features, "evaluation") ? (
                    <AreaOff area="evaluation" />
                ) : (
                    // Why: wraps the whole view, not the charts alone: they stand above the filters, so arriving later they would push an open list off the screen
                    <Suspense fallback={<Loader aria-label="Loading the charts" />}>
                        <AgentView
                            agent={agent}
                            versions={versions}
                            passRates={passRates}
                            groups={groups}
                            tags={tags}
                            reload={reload}
                            onChanged={() => {
                                setReload(reload + 1);
                            }}
                        />
                    </Suspense>
                )
            }
        </Page>
    );
}

type Agent = Awaited<ReturnType<typeof fetchAgent>>;
type PassRates = Awaited<ReturnType<typeof fetchPassRates>>;

function AgentView({
    agent,
    versions,
    passRates,
    groups,
    tags,
    reload,
    onChanged,
}: {
    agent: Agent;
    versions: AgentVersion[];
    passRates: PassRates;
    groups: RunGroupSummary[];
    tags: TagUsage[];
    reload: number;
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
        `${reload}`,
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
    // Why: a model used only in sweeps is not offered while the runs table lists runs in no group
    const offered = offeredVersions(versions, versionGroupingFor(groupFilter), models, dataVersions, versionFilter);
    const toCompare = runsToCompare(runs, selected);
    // Why: a newer version the filters hide must not close problems of the shown ones; with none shown, nothing is asked
    const considered = consideredVersionIds(versions, showArchived, models, dataVersions, versionFilter);
    const problemsAgent = considered.length === 0 ? null : agent.name;
    const { checks: unstable, arrived: checksArrived } = useUnstableChecks(problemsAgent, considered, false, dataVersions);
    const { errors, arrived: errorsArrived } = useAgentErrors(problemsAgent, considered, dataVersions);
    const trendPoints = visibleRuns(passRates, versions, showArchived, models, dataVersions, versionFilter);
    // Why: the Problems cards stand above the filters, so arriving later they would push an open list off the screen
    if (!checksArrived || !errorsArrived) return <Loader aria-label="Loading the problems" />;
    return (
        <Stack gap="lg">
            <Anchor href="/">← Agents</Anchor>
            <div className="agent-layout">
                <div className="agent-layout-header">
                    <Paper withBorder p="md">
                        <Stack gap={4}>
                            <Group justify="space-between">
                                <Title order={2}>{agent.name}</Title>
                                <Anchor href={versionsInEvaluationHref(agent.name)}>Versions in evaluation</Anchor>
                            </Group>
                            <Text c="dimmed">
                                {agent.endpointUrl} · created {formatTime(agent.createdAt)}
                            </Text>
                            <Text c="dimmed">
                                versions {agent.versions} · scenarios {agent.scenarios} · checks {agent.checks}
                            </Text>
                        </Stack>
                    </Paper>
                </div>

                <div className="agent-layout-analytics">
                    <Stack gap="lg">
                        {(unstable.length > 0 || errors.length > 0) && (
                            <Stack gap="sm">
                                <Title order={3}>
                                    <HeaderHelp label="Problems" help={PROBLEMS_SECTION_HELP} />
                                </Title>
                                <div className="agent-health-cards">
                                    <UnstableChecksCard checks={unstable} />
                                    <AgentErrorsCard errors={errors} />
                                </div>
                            </Stack>
                        )}
                        <Trends points={trendPoints} />
                    </Stack>
                </div>

                <Stack gap="lg" className="agent-layout-main">
                    <RunGroupsSection agentName={agent.name} groups={groups} onChanged={onChanged} />
                    <Title order={3}>
                        <HeaderHelp label="Runs" help={RUNS_SECTION_HELP} />
                    </Title>
                    <Group justify="space-between" align="flex-end">
                        <Group align="flex-end">
                            <MultiSelect
                                label={<HeaderHelp label="Model" help={MODEL_HELP} />}
                                placeholder={models.length === 0 ? "All models" : undefined}
                                data={modelsOf(offered)}
                                value={models}
                                onChange={filtered(setModels)}
                                clearable
                                miw={MODEL_FILTER_MIN_WIDTH}
                            />
                            <MultiSelect
                                label={<HeaderHelp label="Data version" help={DATA_VERSION_HELP} />}
                                placeholder={dataVersions.length === 0 ? "All data versions" : undefined}
                                data={dataVersionOptions(offered)}
                                value={dataVersions}
                                onChange={filtered(setDataVersions)}
                                clearable
                                miw={MODEL_FILTER_MIN_WIDTH}
                            />
                            <MultiSelect
                                label={<HeaderHelp label="Versions" help={VERSIONS_FILTER_HELP} />}
                                placeholder={versionFilter.length === 0 ? "All versions" : undefined}
                                data={versionOptions(offered)}
                                value={versionFilter}
                                onChange={filtered(setVersionFilter)}
                                clearable
                                miw={MODEL_FILTER_MIN_WIDTH}
                            />
                            <Select
                                label={<HeaderHelp label="Group" help={GROUP_HELP} />}
                                data={filterOptions(groups, groupFilter)}
                                value={filterValue(groupFilter)}
                                onChange={filtered((value: string | null) => {
                                    setGroupFilter(filterOfValue(value));
                                })}
                                allowDeselect={false}
                                miw={MODEL_FILTER_MIN_WIDTH}
                            />
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
                        </Group>
                        <Button
                            disabled={!canCompare(toCompare.length)}
                            title={`Select ${COMPARE_MIN_RUNS} to ${COMPARE_MAX_RUNS} runs to compare`}
                            onClick={() => {
                                window.location.assign(compareHref(toCompare));
                            }}
                        >
                            Compare ({toCompare.length})
                        </Button>
                    </Group>
                    {error !== null && (
                        <Alert color="red" title="Could not load the runs">
                            {error}
                        </Alert>
                    )}
                    {runs.length === 0 ? (
                        loading ? (
                            <Loader aria-label="Loading runs" />
                        ) : (
                            error === null && (
                                <Text c="dimmed">
                                    {versionIds !== null
                                        ? "No run matches the filters — archived versions are hidden unless the Archived chip is on."
                                        : groupFilter.kind === "none" && groups.length > 0
                                          ? "This agent has no runs outside groups yet — runs started in a group are listed under Any group or All runs."
                                          : "This agent has no runs yet."}
                                </Text>
                            )
                        )
                    ) : (
                        <Paper withBorder p="md">
                            <Table.ScrollContainer minWidth={RUNS_TABLE_MIN_WIDTH}>
                                <Table highlightOnHover>
                                    <Table.Thead>
                                        <Table.Tr>
                                            <Table.Th />
                                            <Table.Th className="no-wrap">
                                                <HeaderHelp label="Run" help={RUN_ID_HELP} />
                                            </Table.Th>
                                            <Table.Th className="no-wrap">
                                                <HeaderHelp label="Status" help={RUN_STATUS_HELP} />
                                            </Table.Th>
                                            <Table.Th>
                                                <HeaderHelp label="Tags" help={TAG_HELP} />
                                            </Table.Th>
                                            <Table.Th>
                                                <HeaderHelp label="Group" help={RUN_GROUP_HELP} />
                                            </Table.Th>
                                            <Table.Th>
                                                <HeaderHelp label="Version" help={VERSION_HELP} />
                                            </Table.Th>
                                            <Table.Th className="no-wrap">
                                                <HeaderHelp label="Checks" help={RUN_CHECKS_HELP} />
                                            </Table.Th>
                                            <Table.Th className="no-wrap">
                                                <HeaderHelp label="Samples" help={RUN_SAMPLES_HELP} />
                                            </Table.Th>
                                            <Table.Th className="no-wrap">
                                                <HeaderHelp label="Created" help={RUN_CREATED_HELP} />
                                            </Table.Th>
                                            <Table.Th className="no-wrap">
                                                <HeaderHelp label="Finished" help={RUN_FINISHED_HELP} />
                                            </Table.Th>
                                        </Table.Tr>
                                    </Table.Thead>
                                    <Table.Tbody>
                                        {runs.map((run) => (
                                            <Table.Tr key={run.id}>
                                                <Table.Td>
                                                    <Checkbox
                                                        aria-label={`Select run ${shortId(run.id)}`}
                                                        checked={selected.has(run.id)}
                                                        onChange={(event) => {
                                                            const next = new Set(selected);
                                                            if (event.currentTarget.checked) next.add(run.id);
                                                            else next.delete(run.id);
                                                            setSelected(next);
                                                        }}
                                                    />
                                                </Table.Td>
                                                <Table.Td className="no-wrap">
                                                    <Anchor href={runHref(run.id)}>{shortId(run.id)}</Anchor>
                                                </Table.Td>
                                                <Table.Td className="no-wrap">
                                                    <Stack gap={4} align="flex-start">
                                                        <Badge color={statusColor(run.status)}>{statusLabel(run.status)}</Badge>
                                                        {run.reference === true && (
                                                            <Badge variant="outline" color="teal">
                                                                Reference
                                                            </Badge>
                                                        )}
                                                        {run.recheckOf != null && (
                                                            <Badge variant="outline" color="gray">
                                                                Re-check
                                                            </Badge>
                                                        )}
                                                    </Stack>
                                                </Table.Td>
                                                <Table.Td>{run.tags?.length ? run.tags.join(", ") : "—"}</Table.Td>
                                                <Table.Td className="wrap-key">
                                                    {run.group == null ? (
                                                        "—"
                                                    ) : (
                                                        <Anchor href={compareGroupHref(run.group, agent.name)}>{run.group}</Anchor>
                                                    )}
                                                </Table.Td>
                                                <Table.Td>
                                                    <Anchor href={versionInUseHref(agent.name, run.agentVersionId, run.group != null)}>
                                                        {runLabel(
                                                            run,
                                                            versions.find((version) => version.id === run.agentVersionId)?.label,
                                                        )}
                                                    </Anchor>
                                                </Table.Td>
                                                <Table.Td className="no-wrap">
                                                    <ChecksCount run={run} />
                                                </Table.Td>
                                                <Table.Td className="no-wrap">
                                                    <SamplesCount run={run} />
                                                </Table.Td>
                                                <Table.Td className="no-wrap">{formatTime(run.createdAt)}</Table.Td>
                                                <Table.Td className="no-wrap">{formatTime(run.finishedAt)}</Table.Td>
                                            </Table.Tr>
                                        ))}
                                    </Table.Tbody>
                                </Table>
                            </Table.ScrollContainer>
                            <Group justify="center" mt="md">
                                <ListPagination
                                    totalCount={totalCount}
                                    page={page}
                                    pageSize={RUNS_PAGE_SIZE}
                                    onChange={(next) => {
                                        setRequestedPage(next);
                                        setSelected(new Set());
                                    }}
                                />
                            </Group>
                        </Paper>
                    )}

                    <ConfigurationSection endpointUrl={agent.endpointUrl} samples={agent.samples} regression={agent.regression} />
                </Stack>
            </div>
        </Stack>
    );
}
