import { Anchor, Badge, Button, cn, formatDateTime, Tooltip } from "@maniumek/suwfrontkit";

import { DataVersion } from "../../dataVersion/DataVersion";
import { statusColor, statusLabel } from "../runs/results";
import type { VariantRun } from "./api";
import { columnTitle, runTagsText } from "./variants";
import { versionInUseHref, compareGroupHref, runHref } from "../../routes/links";
import { shortId } from "../../format/names";

export interface BaselineInfo {
    isBaseline: boolean;
    onToggleBaseline: () => void;
}

export function ColumnHeader({ agent, variant, baseline }: { agent: string; variant: VariantRun; baseline: BaselineInfo | null }) {
    const { run } = variant;
    const labelled = run.label?.trim() ? true : false;
    return (
        <div className="flex flex-col gap-0.5 text-xs font-normal">
            <p className={cn("text-sm font-semibold wrap-anywhere", !labelled && "text-muted-foreground")}>{columnTitle(variant)}</p>
            <p>
                <Anchor href={versionInUseHref(agent, run.agentVersionId, run.group != null)}>Version {shortId(run.agentVersionId)}</Anchor>
            </p>
            <p>
                <Anchor href={runHref(run.id)}>Run {shortId(run.id)}</Anchor>
            </p>
            <p className="wrap-key">Models: {variant.modelNames.length === 0 ? "unknown" : variant.modelNames.join(", ")}</p>
            {run.dataVersion != null && <DataVersion dataVersion={run.dataVersion} description={run.dataDescription} />}
            <p className="wrap-key">Tags: {runTagsText(run.tags)}</p>
            {run.group != null && (
                <p className="wrap-key">
                    <Anchor href={compareGroupHref(run.group, agent)}>Group {run.group}</Anchor>
                </p>
            )}
            {run.note?.trim() && (
                <>
                    <Tooltip label={<span className="whitespace-pre-wrap">{run.note}</span>}>
                        <p className="no-print line-clamp-1">Note: {run.note}</p>
                    </Tooltip>
                    <p className="print-only wrap-key">Note: {run.note}</p>
                </>
            )}
            <p className="no-wrap text-muted-foreground">{formatDateTime(run.createdAt)}</p>
            {run.status !== "Done" && (
                <Badge className="w-fit" variant={statusColor(run.status)}>
                    {statusLabel(run.status)}
                </Badge>
            )}
            {baseline !== null && <BaselineButton baseline={baseline} runId={run.id} />}
        </div>
    );
}

function BaselineButton({ baseline, runId }: { baseline: BaselineInfo; runId: string }) {
    const { isBaseline } = baseline;
    return (
        <>
            {isBaseline && <Badge className="print-only w-fit">Baseline</Badge>}
            <span className="no-print">
                <BaselineToggle baseline={baseline} runId={runId} />
            </span>
        </>
    );
}

function BaselineToggle({ baseline, runId }: { baseline: BaselineInfo; runId: string }) {
    const { isBaseline } = baseline;
    return (
        <Tooltip label="Click to unselect the baseline" disabled={!isBaseline}>
            <Button
                size="xs"
                variant={isBaseline ? "default" : "outline"}
                className="w-fit"
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
