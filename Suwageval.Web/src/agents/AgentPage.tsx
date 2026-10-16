import { Alert, Anchor, Badge, Button, Checkbox, Group, Loader, MultiSelect, Paper, Stack, Table, Text, Title } from "@mantine/core";
import { lazy, Suspense, useState } from "react";

import { useUnstableChecks } from "../runs/useUnstableChecks";
import { formatTime, num, progress, statusColor, statusLabel } from "../runs/results";
import { fetchAgent, fetchPassRates, fetchVersions, type AgentVersion } from "./api";
import { HeaderHelp } from "./HeaderHelp";
import { Page } from "./Page";
import { useLoaded } from "./useLoaded";
import { useRuns } from "./useRuns";
import {
    allowedVersionIds,
    canCompare,
    compareHref,
    dataVersionsOf,
    modelsOf,
    runHref,
    runsToCompare,
    shortId,
    versionHref,
    visibleRuns,
    COMPARE_MAX_RUNS,
    COMPARE_MIN_RUNS,
} from "./view";
import { UnstableChecksCard } from "./UnstableChecksCard";
import { VersionsSection } from "./VersionsSection";

// The chart library is the largest part of the app: it loads only when an agent's page is opened.
/** What the filters and the runs table's columns mean, said on hover over their labels. */
const MODEL_HELP =
    "Show only runs of agent versions that use these models (any of the system's LLM agents). The chart and the unstable checks follow the filter.";
const DATA_VERSION_HELP =
    "Show only runs of agent versions whose retrieval data is one of these: the dataVersion the agent declares, a fingerprint of what its retrieval can return (content and indexing). New data is a new agent version. Agents without retrieval have none. The chart and the unstable checks follow the filter.";
const TAG_HELP = "The scenario tag the run was limited to (run --tag); — means every scenario of the agent ran.";
const VERSION_HELP =
    "The agent version the run used: its contract as the agent served it when the run started (LLM agents, prompts, models, tools, events, data version). Any change there is a new version; open it to see the contract, or tick two in Versions to compare them.";
const CHECKS_HELP =
    "Checks passed and failed in this run. A check is one condition on the agent's answer, written in a scenario file: a tool called or not, an event, a text, a cited source, a judge's verdict. It passes when its share of passing samples reaches its threshold.";
const SAMPLES_HELP =
    "Answers of the agent in this run: done / planned. A sample is one play of a scenario's conversation; a scenario is played as many times as its samples field says (1 by default), so a check's pass rate is the share of its samples that passed.";

const PassRateTrend = lazy(() => import("./PassRateTrend").then((module) => ({ default: module.PassRateTrend })));

const MODEL_FILTER_MIN_WIDTH = 260;
const RUNS_TABLE_MIN_WIDTH = 940;

/** `/agents/<name>`: the agent, its runs (newest first) and its versions. */
export function AgentPage({ name }: { name: string }) {
    // Bumped after versions are archived or restored: the lists are loaded again (the page stays until the new ones arrive).
    const [reload, setReload] = useState(0);
    const state = useLoaded(`${name}/${reload}`, async (signal) => {
        const [agent, versions, passRates] = await Promise.all([
            fetchAgent(name, signal),
            fetchVersions(name, signal),
            fetchPassRates(name, signal),
        ]);
        return { agent, versions, passRates };
    });

    return (
        <Page state={state} notFound={`There is no agent ${name}.`}>
            {({ agent, versions, passRates }) => (
                <AgentView
                    agent={agent}
                    versions={versions}
                    passRates={passRates}
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
    reload,
    onChanged,
}: {
    agent: Agent;
    versions: AgentVersion[];
    passRates: PassRates;
    reload: number;
    onChanged: () => void;
}) {
    const [showArchived, setShowArchived] = useState(false);
    // The model filter (empty: every model) and the runs ticked for comparison.
    const [models, setModels] = useState<string[]>([]);
    const [dataVersions, setDataVersions] = useState<string[]>([]);
    const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
    // The server pages the runs, so the filters reach it as the versions they allow (none sent: every version).
    const versionIds = allowedVersionIds(versions, showArchived, models, dataVersions);
    const { runs, hasMore, loading, loadingMore, error, loadMore } = useRuns(agent.name, versionIds?.join(",") ?? null, `${reload}`);
    // Only what is listed counts: a run the filter hides is not compared.
    const toCompare = runsToCompare(runs, selected);
    // Fetched once for all versions; the filters hide the rows of versions they leave out, as they do in the chart.
    const unstable = visibleRuns(useUnstableChecks(agent.name, []), versions, showArchived, models, dataVersions);
    return (
        <Stack gap="lg">
            <Anchor href="/">← Agents</Anchor>
            {/* Wide screens: the agent and its runs on the left, the analytics beside them (app.css). */}
            <div className="agent-layout">
                <div className="agent-layout-header">
                    <Paper withBorder p="md">
                        <Stack gap={4}>
                            <Title order={2}>{agent.name}</Title>
                            <Text c="dimmed">
                                {agent.endpointUrl} · created {formatTime(agent.createdAt)}
                            </Text>
                            <Text c="dimmed">
                                versions {num(agent.versions)} · scenarios {num(agent.scenarios)} · checks {num(agent.checks)}
                            </Text>
                        </Stack>
                    </Paper>
                </div>

                <div className="agent-layout-analytics">
                    <Stack gap="lg">
                        <Suspense fallback={null}>
                            <PassRateTrend points={visibleRuns(passRates, versions, showArchived, models, dataVersions)} />
                        </Suspense>
                        <UnstableChecksCard checks={unstable} />
                    </Stack>
                </div>

                <Stack gap="lg" className="agent-layout-main">
                    <Title order={3}>Runs</Title>
                    <Group justify="space-between" align="flex-end">
                        <Group align="flex-end">
                            <MultiSelect
                                label={<HeaderHelp label="Model" help={MODEL_HELP} />}
                                placeholder={models.length === 0 ? "All models" : undefined}
                                data={modelsOf(versions)}
                                value={models}
                                onChange={setModels}
                                clearable
                                miw={MODEL_FILTER_MIN_WIDTH}
                            />
                            <MultiSelect
                                label={<HeaderHelp label="Data version" help={DATA_VERSION_HELP} />}
                                placeholder={dataVersions.length === 0 ? "All data versions" : undefined}
                                data={dataVersionsOf(versions)}
                                value={dataVersions}
                                onChange={setDataVersions}
                                clearable
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
                                    {versionIds === null
                                        ? "This agent has no runs yet."
                                        : "No run matches the filters — archived versions are hidden unless Show archived is on."}
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
                                            <Table.Th>Run</Table.Th>
                                            <Table.Th>Status</Table.Th>
                                            <Table.Th>
                                                <HeaderHelp label="Tag" help={TAG_HELP} />
                                            </Table.Th>
                                            <Table.Th>
                                                <HeaderHelp label="Version" help={VERSION_HELP} />
                                            </Table.Th>
                                            <Table.Th>
                                                <HeaderHelp label="Checks" help={CHECKS_HELP} />
                                            </Table.Th>
                                            <Table.Th>
                                                <HeaderHelp label="Samples" help={SAMPLES_HELP} />
                                            </Table.Th>
                                            <Table.Th>Created</Table.Th>
                                            <Table.Th>Finished</Table.Th>
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
                                                <Table.Td>
                                                    <Anchor href={runHref(run.id)}>{shortId(run.id)}</Anchor>
                                                </Table.Td>
                                                <Table.Td>
                                                    <Group gap={4}>
                                                        <Badge color={statusColor(run.status)}>{statusLabel(run.status)}</Badge>
                                                        {run.recheckOf != null && (
                                                            <Badge variant="outline" color="gray">
                                                                Re-check
                                                            </Badge>
                                                        )}
                                                    </Group>
                                                </Table.Td>
                                                <Table.Td>{run.tag ?? "—"}</Table.Td>
                                                <Table.Td>
                                                    <Anchor href={versionHref(agent.name, run.agentVersionId)}>
                                                        {shortId(run.agentVersionId)}
                                                    </Anchor>
                                                </Table.Td>
                                                <Table.Td>
                                                    <Group gap={4}>
                                                        <Text span c="green">
                                                            {num(run.checksPassed)} passed
                                                        </Text>
                                                        <Text span c={num(run.checksFailed) > 0 ? "red" : "dimmed"}>
                                                            {num(run.checksFailed)} failed
                                                        </Text>
                                                    </Group>
                                                </Table.Td>
                                                <Table.Td>{progress(run)}</Table.Td>
                                                <Table.Td>{formatTime(run.createdAt)}</Table.Td>
                                                <Table.Td>{formatTime(run.finishedAt)}</Table.Td>
                                            </Table.Tr>
                                        ))}
                                    </Table.Tbody>
                                </Table>
                            </Table.ScrollContainer>
                            {hasMore && (
                                <Group justify="center" mt="md">
                                    <Button variant="light" disabled={loading || loadingMore} loading={loadingMore} onClick={loadMore}>
                                        Load more
                                    </Button>
                                </Group>
                            )}
                        </Paper>
                    )}

                    <VersionsSection
                        agentName={agent.name}
                        versions={versions}
                        showArchived={showArchived}
                        onShowArchivedChange={setShowArchived}
                        onChanged={onChanged}
                    />
                </Stack>
            </div>
        </Stack>
    );
}
