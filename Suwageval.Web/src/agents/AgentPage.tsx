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

import { ListPagination } from "../pagination/ListPagination";
import { usePageParam } from "../pagination/usePageParam";
import { RUNS_PAGE_SIZE } from "../runs/paging";
import { useUnstableChecks } from "../runs/useUnstableChecks";
import { ChecksCount, SamplesCount } from "../runs/RunCounts";
import {
    formatTime,
    RUN_CHECKS_HELP,
    RUN_SAMPLES_HELP,
    runHref,
    runLabel,
    shortId,
    statusColor,
    statusLabel,
    compareGroupHref,
} from "../runs/results";
import { fetchAgent, fetchPassRates, fetchRunGroups, fetchVersions, type AgentVersion, type RunGroupSummary } from "./api";
import { AgentErrorsCard } from "./AgentErrorsCard";
import { ConfigurationSection } from "./ConfigurationSection";
import { DeleteAgentModal } from "./DeleteAgentModal";
import { HeaderHelp } from "../runs/HeaderHelp";
import { filterOfValue, filterOptions, filterValue } from "./groupFilter";
import { Page } from "./Page";
import { RunGroupsSection } from "./RunGroupsSection";
import { useAgentErrors } from "./useAgentErrors";
import { useGroupFilter } from "./useGroupFilter";
import { useRequest } from "../api/useRequest";
import { useRuns } from "./useRuns";
import {
    allowedVersionIds,
    canCompare,
    consideredVersionIds,
    compareHref,
    dataVersionsOf,
    modelsOf,
    runsToCompare,
    versionHref,
    versionOptions,
    visibleRuns,
    COMPARE_MAX_RUNS,
    COMPARE_MIN_RUNS,
    RUN_CREATED_HELP,
    RUN_FINISHED_HELP,
    RUN_GROUP_HELP,
    RUN_ID_HELP,
    RUN_STATUS_HELP,
    RUNS_SECTION_HELP,
    VERSION_HELP,
} from "./view";
import { UnstableChecksCard } from "./UnstableChecksCard";
import { VersionsSection } from "./versions/VersionsSection";

// The chart library is the largest part of the app: it loads only when an agent's page is opened.
/** What the filters and the runs table's columns mean, said on hover over their labels. */
const MODEL_HELP =
    "Show only runs of agent versions that use these models (any of the system's LLM agents). The chart, the unstable checks and the agent errors follow the filter.";
const DATA_VERSION_HELP =
    "Show only runs of agent versions whose retrieval data is one of these: the dataVersion the agent declares, a fingerprint of what its retrieval can return (content and indexing). New data is a new agent version. Agents without retrieval have none. The chart, the unstable checks and the agent errors follow the filter.";
const VERSIONS_FILTER_HELP =
    "Show only runs of these agent versions (newest first; empty means all). The chart, the unstable checks and the agent errors follow the filter.";
const GROUP_HELP =
    "Which runs to list by group (run --group): those in no group (the default), those in any group, all runs, or one group. Group runs are experiments, kept out of the regression history and the pass-rate trend. Open a group from the Groups section to compare its runs as variants.";
const TAG_HELP = "The scenario tags the run was limited to (run --tag, repeatable); — means every scenario of the agent ran.";

const PassRateTrend = lazy(() => import("./trend/PassRateTrend").then((module) => ({ default: module.PassRateTrend })));
const TurnTimeTrend = lazy(() => import("./trend/TurnTimeTrend").then((module) => ({ default: module.TurnTimeTrend })));

const MODEL_FILTER_MIN_WIDTH = 260;
const RUNS_TABLE_MIN_WIDTH = 1100;

/** `/agents/<name>`: the agent, its runs (newest first) and its versions. */
export function AgentPage({ name }: { name: string }) {
    // Bumped after versions are archived or restored: the lists are loaded again (the page stays until the new ones arrive).
    const [reload, setReload] = useState(0);
    const state = useRequest(`${name}/${reload}`, async (signal) => {
        const [agent, versions, passRates, groups] = await Promise.all([
            fetchAgent(name, signal),
            fetchVersions(name, signal),
            fetchPassRates(name, signal),
            fetchRunGroups(name, signal),
        ]);
        return { agent, versions, passRates, groups };
    });

    return (
        <Page state={state} notFound={`There is no agent ${name}.`}>
            {({ agent, versions, passRates, groups }) => (
                <AgentView
                    agent={agent}
                    versions={versions}
                    passRates={passRates}
                    groups={groups}
                    reload={reload}
                    onChanged={() => {
                        setReload(reload + 1);
                    }}
                />
            )}
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
    reload,
    onChanged,
}: {
    agent: Agent;
    versions: AgentVersion[];
    passRates: PassRates;
    groups: RunGroupSummary[];
    reload: number;
    onChanged: () => void;
}) {
    const [showArchived, setShowArchived] = useState(false);
    const [deleting, setDeleting] = useState(false);
    // The model filter (empty: every model) and the runs ticked for comparison.
    const [models, setModels] = useState<string[]>([]);
    const [dataVersions, setDataVersions] = useState<string[]>([]);
    const [versionFilter, setVersionFilter] = useState<string[]>([]);
    const [groupFilter, setGroupFilter] = useGroupFilter();
    const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
    // The server pages the runs, so the filters reach it as the versions they allow (none sent: every version).
    const versionIds = allowedVersionIds(versions, showArchived, models, dataVersions, versionFilter);
    const [requestedPage, setRequestedPage] = usePageParam("runsPage");
    const { runs, totalCount, page, loading, error } = useRuns(
        agent.name,
        versionIds?.join(",") ?? null,
        `${reload}`,
        groupFilter,
        requestedPage,
        RUNS_PAGE_SIZE,
    );
    // A filter changes which runs there are, so the list starts again from its first page.
    const filtered = <Value,>(change: (value: Value) => void) => {
        return (value: Value) => {
            change(value);
            setRequestedPage(1);
        };
    };
    // Only what is listed counts: a run the filter hides is not compared.
    const toCompare = runsToCompare(runs, selected);
    // The problem cards decide over the versions the filters show: a newer version the filters hide closes nothing. With none shown, nothing is asked.
    const considered = consideredVersionIds(versions, showArchived, models, dataVersions, versionFilter);
    const problemsAgent = considered.length === 0 ? null : agent.name;
    const unstable = useUnstableChecks(problemsAgent, considered);
    const errors = useAgentErrors(problemsAgent, considered);
    const trendPoints = visibleRuns(passRates, versions, showArchived, models, dataVersions, versionFilter);
    return (
        <Stack gap="lg">
            <Anchor href="/">← Agents</Anchor>
            {deleting && (
                <DeleteAgentModal
                    name={agent.name}
                    onClose={() => {
                        setDeleting(false);
                    }}
                    onDeleted={() => {
                        window.location.assign("/");
                    }}
                />
            )}
            {/* Wide screens: the agent and its runs on the left, the analytics beside them (app.css). */}
            <div className="agent-layout">
                <div className="agent-layout-header">
                    <Paper withBorder p="md">
                        <Stack gap={4}>
                            <Group justify="space-between">
                                <Title order={2}>{agent.name}</Title>
                                <Button
                                    color="red"
                                    variant="light"
                                    onClick={() => {
                                        setDeleting(true);
                                    }}
                                >
                                    Delete agent
                                </Button>
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
                        {/* First, so it is never missed below the charts: these failures may be noise. */}
                        {(unstable.length > 0 || errors.length > 0) && (
                            <div className="agent-health-cards">
                                <UnstableChecksCard checks={unstable} />
                                <AgentErrorsCard errors={errors} />
                            </div>
                        )}
                        <Suspense fallback={null}>
                            <PassRateTrend points={trendPoints} />
                            <TurnTimeTrend points={trendPoints} />
                        </Suspense>
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
                                data={modelsOf(versions)}
                                value={models}
                                onChange={filtered(setModels)}
                                clearable
                                miw={MODEL_FILTER_MIN_WIDTH}
                            />
                            <MultiSelect
                                label={<HeaderHelp label="Data version" help={DATA_VERSION_HELP} />}
                                placeholder={dataVersions.length === 0 ? "All data versions" : undefined}
                                data={dataVersionsOf(versions)}
                                value={dataVersions}
                                onChange={filtered(setDataVersions)}
                                clearable
                                miw={MODEL_FILTER_MIN_WIDTH}
                            />
                            <MultiSelect
                                label={<HeaderHelp label="Versions" help={VERSIONS_FILTER_HELP} />}
                                placeholder={versionFilter.length === 0 ? "All versions" : undefined}
                                data={versionOptions(versions)}
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
                                        ? "No run matches the filters — archived versions are hidden unless Show archived is on."
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
                                                    <Group gap={4} wrap="nowrap">
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
                                                    </Group>
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
                                                    <Anchor href={versionHref(agent.name, run.agentVersionId)}>
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

                    <VersionsSection
                        agentName={agent.name}
                        versions={versions}
                        showArchived={showArchived}
                        onShowArchivedChange={filtered(setShowArchived)}
                        onChanged={onChanged}
                    />

                    <ConfigurationSection endpointUrl={agent.endpointUrl} samples={agent.samples} regression={agent.regression} />
                </Stack>
            </div>
        </Stack>
    );
}
