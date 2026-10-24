import { Anchor, Badge, Box, Button, Stack, Text, Tooltip } from "@mantine/core";

import { DataVersion } from "../../dataVersion/DataVersion";
import type { AgentVersion } from "../../agents/api";
import { statusColor, statusLabel } from "../runs/results";
import type { VariantRun } from "./api";
import { columnTitle, runTagsText } from "./variants";
import { versionInUseHref, compareGroupHref, runHref } from "../../routes/links";
import { formatTime } from "../../format/time";
import { shortId } from "../../format/names";

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
    return (
        <Stack gap={2}>
            <Text fw={600} c={labelled ? "inherit" : "dimmed"} className="wrap-key">
                {columnTitle(variant)}
            </Text>
            <Text size="xs" fw="normal">
                <Anchor href={versionInUseHref(agent, run.agentVersionId, run.group != null)}>Version {shortId(run.agentVersionId)}</Anchor>
            </Text>
            <Text size="xs" fw="normal">
                <Anchor href={runHref(run.id)}>Run {shortId(run.id)}</Anchor>
            </Text>
            <Text size="xs" fw="normal" className="wrap-key">
                Models: {variant.modelNames.length === 0 ? "unknown" : variant.modelNames.join(", ")}
            </Text>
            {version?.dataVersion != null && <DataVersion dataVersion={version.dataVersion} description={version.dataDescription} />}
            <Text size="xs" fw="normal" className="wrap-key">
                Tags: {runTagsText(run.tags)}
            </Text>
            {run.group != null && (
                <Text size="xs" fw="normal" className="wrap-key">
                    <Anchor href={compareGroupHref(run.group, agent)}>Group {run.group}</Anchor>
                </Text>
            )}
            {run.note?.trim() && (
                <>
                    {/* One line on the screen, the whole note in its tooltip; on paper the whole note, wrapped. */}
                    <Tooltip label={run.note} multiline maw={360} style={{ whiteSpace: "pre-wrap" }}>
                        <Text size="xs" fw="normal" lineClamp={1} className="no-print">
                            Note: {run.note}
                        </Text>
                    </Tooltip>
                    <Text size="xs" fw="normal" className="print-only wrap-key">
                        Note: {run.note}
                    </Text>
                </>
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
    // On paper the baseline is named, and the button that picks one is gone.
    return (
        <>
            {isBaseline && (
                <Badge w="fit-content" className="print-only">
                    Baseline
                </Badge>
            )}
            <Box component="span" className="no-print">
                <BaselineToggle baseline={baseline} runId={runId} />
            </Box>
        </>
    );
}

function BaselineToggle({ baseline, runId }: { baseline: BaselineInfo; runId: string }) {
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
