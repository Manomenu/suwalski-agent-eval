import { Anchor, Badge, Button, Group, Stack, Text, Tooltip } from "@mantine/core";

import type { AgentVersion } from "../agents/api";
import { dataText, versionHref } from "../agents/view";
import { ModelJudgedIcon } from "../checks/ModelJudgedIcon";
import { formatTime, runHref, shortId, statusColor, statusLabel, variantsGroupHref } from "../runs/results";
import type { ComparedRun, VariantRun } from "./api";
import { timeDeltaText, trendColor, TIME_DELTA_HELP } from "./compare";
import { columnTitle, runTagsText } from "./variants";

const HELP_WIDTH = 280;

/** What the baseline brings to a column: its marker or button and the judge and turn time changes the server computed against it. */
export interface BaselineInfo {
    compared: ComparedRun;
    baselineJudgeModel: string | null;
    isBaseline: boolean;
    onUseAsBaseline: () => void;
}

/** The turn time changes of a run against the baseline's, as the server computed them: median and p95. */
function timeChanges({ medianChange, p95Change }: ComparedRun) {
    return [
        ...(medianChange == null ? [] : [{ label: "median", change: medianChange }]),
        ...(p95Change == null ? [] : [{ label: "p95", change: p95Change }]),
    ];
}

/**
 * The header of one run's column. Every line says what it is — label, version, run, models, tags, group, date — and a link's text says
 * where it leads: the version's page, the run's page, the group's page.
 */
export function ColumnHeader({
    agent,
    variant,
    version,
    baseline,
}: {
    agent: string;
    variant: VariantRun;
    version: AgentVersion | undefined;
    baseline: BaselineInfo | null;
}) {
    const { run } = variant;
    const labelled = run.label?.trim() ? true : false;
    const data = version === undefined ? null : dataText(version);
    return (
        <Stack gap={2}>
            <Text fw={600} c={labelled ? "inherit" : "dimmed"} className="wrap-key">
                {columnTitle(variant)}
            </Text>
            <Text size="xs" fw="normal">
                <Anchor href={versionHref(agent, run.agentVersionId)}>Version {shortId(run.agentVersionId)}</Anchor>
            </Text>
            <Text size="xs" fw="normal">
                <Anchor href={runHref(run.id)}>Run {shortId(run.id)}</Anchor>
            </Text>
            <Text size="xs" fw="normal" className="wrap-key">
                Models: {variant.modelNames.length === 0 ? "unknown" : variant.modelNames.join(", ")}
            </Text>
            {data !== null && (
                <Text size="xs" fw="normal">
                    {data}
                </Text>
            )}
            <Text size="xs" fw="normal" className="wrap-key">
                Tags: {runTagsText(run.tags)}
            </Text>
            {run.group != null && (
                <Text size="xs" fw="normal" className="wrap-key">
                    <Anchor href={variantsGroupHref(run.group, agent)}>Group {run.group}</Anchor>
                </Text>
            )}
            <Text size="xs" c="dimmed" fw="normal" className="no-wrap">
                {formatTime(run.createdAt)}
            </Text>
            {run.status !== "Done" && (
                <Badge size="xs" w="fit-content" color={statusColor(run.status)}>
                    {statusLabel(run.status)}
                </Badge>
            )}
            {baseline !== null && <BaselineLines baseline={baseline} runId={run.id} />}
        </Stack>
    );
}

function BaselineLines({ baseline, runId }: { baseline: BaselineInfo; runId: string }) {
    const { compared, isBaseline } = baseline;
    return (
        <>
            {compared.judgeModel != null && (
                <Group gap={4} wrap="nowrap">
                    <ModelJudgedIcon model={compared.judgeModel} />
                    <Text size="xs" fw="normal">
                        judge {compared.judgeModel}
                    </Text>
                    {compared.judgeChanged && (
                        <Tooltip
                            label={`The baseline was judged by ${baseline.baselineJudgeModel ?? ""}: a different verdict may come from the judge, not the agent`}
                            multiline
                            w={HELP_WIDTH}
                        >
                            <Badge size="xs" variant="light" color="orange" style={{ cursor: "help" }}>
                                other judge
                            </Badge>
                        </Tooltip>
                    )}
                </Group>
            )}
            {!isBaseline && (
                <Tooltip label={TIME_DELTA_HELP} multiline w={HELP_WIDTH}>
                    <Group gap="xs" wrap="nowrap" w="fit-content" style={{ cursor: "help" }}>
                        {timeChanges(compared).map(({ label, change }) => (
                            <Text key={label} size="xs" fw="normal" c={trendColor(change.trend)}>
                                {label} {timeDeltaText(change)}
                            </Text>
                        ))}
                    </Group>
                </Tooltip>
            )}
            {isBaseline ? (
                <Badge variant="filled" w="fit-content">
                    Baseline
                </Badge>
            ) : (
                <Button
                    size="compact-xs"
                    variant="light"
                    w="fit-content"
                    aria-label={`Use run ${shortId(runId)} as the baseline`}
                    onClick={baseline.onUseAsBaseline}
                >
                    Use as baseline
                </Button>
            )}
        </>
    );
}
