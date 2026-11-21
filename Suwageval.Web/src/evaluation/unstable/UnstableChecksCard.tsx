import { Anchor, Paper, Tooltip } from "@maniumek/suwfrontkit";

import { UNSTABLE_EXPLANATION, UnstableCheckIcon } from "../checks/UnstableCheckIcon";
import { ListPagination } from "../../pagination/ListPagination";
import { pageOfItems } from "../../pagination/pages";
import { usePageParam } from "../../pagination/usePageParam";
import { passedOfRuns, UNSTABLE_CARD_RULE } from "./stability";
import { runHref } from "../../routes/links";
import { versionTitle } from "../../format/names";
import { type UnstableCheck } from "./api";
import { UNSTABLE_CARD_PAGE_SIZE } from "./stability";

export function UnstableChecksCard({ checks }: { checks: UnstableCheck[] }) {
    const [requestedPage, setRequestedPage] = usePageParam("unstablePage");
    if (checks.length === 0) return null;
    const { items: listed, page } = pageOfItems(checks, requestedPage, UNSTABLE_CARD_PAGE_SIZE);
    return (
        <Paper className="flex flex-col gap-3">
            <Tooltip
                label={
                    <span className="flex flex-col gap-2.5">
                        <span>{UNSTABLE_EXPLANATION}</span>
                        <span>{UNSTABLE_CARD_RULE}</span>
                    </span>
                }
            >
                <div className="flex cursor-help items-center gap-2.5 self-start">
                    <UnstableCheckIcon />
                    <h3 className="text-[22px] font-bold underline decoration-dotted decoration-1">Unstable checks</h3>
                </div>
            </Tooltip>
            <p className="text-sm text-muted-foreground">Passed in some runs of a version and failed in others.</p>
            {listed.map((check) => (
                <div
                    key={JSON.stringify([check.agentVersionId, check.scenarioKey, check.checkKey, check.checkRevision])}
                    className="flex items-baseline justify-between gap-4"
                >
                    <Anchor href={runHref(check.newestFailedRunId)} className="wrap-key">
                        {`${check.scenarioKey} / ${check.checkKey}`}
                    </Anchor>
                    <div className="flex items-baseline gap-2.5">
                        <span className="font-semibold">{passedOfRuns(check)}</span>
                        <span className="text-sm text-muted-foreground">{versionTitle(check.agentVersionId, check.versionLabel)}</span>
                    </div>
                </div>
            ))}
            <ListPagination totalCount={checks.length} page={page} pageSize={UNSTABLE_CARD_PAGE_SIZE} onChange={setRequestedPage} />
        </Paper>
    );
}
