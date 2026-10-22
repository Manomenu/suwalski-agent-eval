import { Anchor, Badge, Button, Stack, Text, Tooltip } from "@mantine/core";

import type { AgentVersion } from "../agents/api";
import { dataText, versionHref } from "../agents/view";
import { compareGroupHref, formatTime, runHref, shortId, statusColor, statusLabel } from "../runs/results";
import type { VariantRun } from "./api";
import { columnTitle, runTagsText } from "./variants";

/**
 * The button that makes a column the baseline (or, on the baseline itself, takes it back). The changes against the baseline are in the
 * table's rows, never here: the header keeps its height, so the button stays where it was clicked.
 */
export interface BaselineInfo {
    isBaseline: boolean;
    onToggleBaseline: () => void;
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
                    <Anchor href={compareGroupHref(run.group, agent)}>Group {run.group}</Anchor>
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
            {baseline !== null && <BaselineButton baseline={baseline} runId={run.id} />}
        </Stack>
    );
}

function BaselineButton({ baseline, runId }: { baseline: BaselineInfo; runId: string }) {
    const { isBaseline } = baseline;
    return (
        <Tooltip label="Click to unselect the baseline" disabled={!isBaseline}>
            <Button
                size="compact-xs"
                variant={isBaseline ? "filled" : "light"}
                w="fit-content"
                aria-pressed={isBaseline}
                aria-label={
                    isBaseline ? `Run ${shortId(runId)} is the baseline: click to remove it` : `Use run ${shortId(runId)} as the baseline`
                }
                onClick={baseline.onToggleBaseline}
            >
                {isBaseline ? "Baseline" : "Use as baseline"}
            </Button>
        </Tooltip>
    );
}
