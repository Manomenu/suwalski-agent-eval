import { Anchor, Button, Paper } from "@maniumek/suwfrontkit";
import { Fragment, useState } from "react";

import type { RunResults } from "./api";
import {
    comparedWithHeading,
    failedOnOtherData,
    groupChecks,
    historyText,
    compareLink,
    stateLabel,
    statedChecks,
    type StatedCheck,
} from "./regression";
import { DataChangedBadge } from "./DataChangedBadge";
import { CheckHistory } from "./CheckHistory";
import { checkAnchor } from "./results";
import { StateBadge } from "./StateBadge";
import { runHref } from "../../routes/links";
import { shortId } from "../../format/names";

function CheckLine({ item, runId }: { item: StatedCheck; runId: string }) {
    const regression = item.check.regression;
    const compare = compareLink(runId, regression);
    const failingSince = regression?.failingSince;
    return (
        <li className="regression-checks-row">
            <Anchor href={`#${checkAnchor(item.scenarioKey, item.check.key)}`} className="wrap-key font-semibold">
                {item.scenarioKey} / {item.check.key}
            </Anchor>
            <div>
                <CheckHistory check={item.check} />
            </div>
            <div className="flex flex-wrap items-center gap-2.5">
                {failedOnOtherData(item.check) && <DataChangedBadge />}
                {failingSince != null && (
                    <span className="text-muted-foreground">
                        failing since <Anchor href={runHref(failingSince)}>run {shortId(failingSince)}</Anchor>
                    </span>
                )}
                {compare !== null && (
                    <Anchor href={compare.href} className="no-print">
                        {compare.label}
                    </Anchor>
                )}
            </div>
        </li>
    );
}

function KnownFailures({ known, runId }: { known: readonly StatedCheck[]; runId: string }) {
    const [opened, setOpened] = useState(false);
    return (
        <>
            <div className="regression-checks-heading flex flex-wrap items-center gap-2.5">
                <StateBadge state="KnownFailure">{stateLabel("KnownFailure")}</StateBadge>
                <span className="text-muted-foreground">({known.length})</span>
                <Button
                    variant="link"
                    size="sm"
                    className="no-print"
                    aria-expanded={opened}
                    onClick={() => {
                        setOpened((open) => !open);
                    }}
                >
                    {opened ? "hide" : "show"}
                </Button>
            </div>
            <ul className={opened ? "regression-checks-group" : "regression-checks-group folded-block"}>
                {known.map((item) => (
                    <CheckLine key={`${item.scenarioKey}/${item.check.key}`} item={item} runId={runId} />
                ))}
            </ul>
        </>
    );
}

export function RegressionSection({ results }: { results: RunResults }) {
    const summary = results.regression;
    if (summary == null) return null;
    const groups = groupChecks(statedChecks(results));
    const runId = results.run.id;
    return (
        <Paper role="region" aria-label={comparedWithHeading(summary)}>
            <div className="flex flex-col gap-3">
                <h3 className="text-[22px] font-bold">{comparedWithHeading(summary)}</h3>
                <p className="text-sm text-muted-foreground">{historyText(summary)}</p>
                <div className="regression-checks">
                    {groups.groups.map((group) => (
                        <Fragment key={group.state}>
                            <div className="regression-checks-heading flex flex-wrap items-center gap-2.5">
                                <StateBadge state={group.state}>{stateLabel(group.state)}</StateBadge>
                                <span className="text-muted-foreground">({group.checks.length})</span>
                            </div>
                            <ul className="regression-checks-group">
                                {group.checks.map((item) => (
                                    <CheckLine key={`${item.scenarioKey}/${item.check.key}`} item={item} runId={runId} />
                                ))}
                            </ul>
                        </Fragment>
                    ))}
                    {groups.known.length > 0 && <KnownFailures known={groups.known} runId={runId} />}
                </div>
            </div>
        </Paper>
    );
}
