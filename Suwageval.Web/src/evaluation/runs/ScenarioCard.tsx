import { useEffect, useState } from "react";
import { Alert, Badge, Paper } from "@maniumek/suwfrontkit";

import { OnPaper } from "../../print/OnPaper";
import { CheckDefinitionButton } from "../checks/CheckDefinitionButton";
import { ModelJudgedIcon } from "../../verdict/ModelJudgedIcon";
import { UnstableCheckIcon } from "../checks/UnstableCheckIcon";
import type { SampleResult, ScenarioRunResult } from "./api";
import { ConversationView } from "./ConversationView";
import { ScenarioDefinitionButton } from "./ScenarioDefinitionButton";
import { CheckHistory } from "./CheckHistory";
import { failedOnOtherData, stateLabel } from "./regression";
import { DataChangedBadge } from "./DataChangedBadge";
import { StateBadge } from "./StateBadge";
import { VerdictBadge } from "../../verdict/VerdictBadge";
import { VerdictMessage } from "../../verdict/VerdictMessage";
import { findUnstable, unstableTooltip } from "../unstable/stability";
import { checkAnchor, checkSummary, failedEvidenceKeys, notJudgedCount, sampleVerdict, samplesToOpen } from "./results";
import { visibleChecks } from "./hidePassed";
import { isSimulated, sampleTitle } from "./simulation";
import { scenarioAnchor } from "../../routes/links";
import { type UnstableCheck } from "../unstable/api";

function SampleRowLabel({ scenario, sample }: { scenario: ScenarioRunResult; sample: SampleResult }) {
    const verdict = sampleVerdict(scenario, sample.sample);
    return (
        <span className="inline-flex flex-wrap items-center gap-2.5">
            <span>{sampleTitle(sample)}</span>
            {verdict.kind === "no output" && <Badge variant="danger">no output</Badge>}
            {verdict.kind === "not simulated" && <Badge variant="warning">not simulated</Badge>}
            {verdict.kind !== "unchecked" && verdict.kind !== "no output" && verdict.kind !== "not simulated" && (
                <VerdictBadge
                    verdict={verdict.kind}
                    label={
                        verdict.kind === "failed"
                            ? `failed: ${verdict.failedChecks} ${verdict.failedChecks === 1 ? "check" : "checks"}`
                            : verdict.kind
                    }
                />
            )}
        </span>
    );
}

export function ScenarioCard({
    scenario,
    versionId,
    unstable,
    hidePassed,
}: {
    scenario: ScenarioRunResult;
    versionId: string;
    unstable: UnstableCheck[];
    hidePassed: boolean;
}) {
    const [openSamples, setOpenSamples] = useState<string[]>([]);
    useEffect(() => {
        const openFromHash = () => {
            const target = scenario.checks.find((check) => `#${checkAnchor(scenario.key, check.key)}` === window.location.hash);
            if (target !== undefined) setOpenSamples((open) => [...new Set([...open, ...samplesToOpen(target)])]);
        };
        openFromHash();
        window.addEventListener("hashchange", openFromHash);
        return () => window.removeEventListener("hashchange", openFromHash);
    }, [scenario]);
    return (
        <Paper id={scenarioAnchor(scenario.key)}>
            <div className="flex flex-col gap-3">
                <div className="flex flex-wrap items-center gap-2.5">
                    <h3 className="wrap-key text-[22px] font-bold">{scenario.key}</h3>
                    {scenario.acGuardrail != null && (
                        <Badge variant="neutral" title="The acceptance criterion this scenario guards on the gate (suwageval gate)">
                            {scenario.acGuardrail.id}
                        </Badge>
                    )}
                    <span className="no-print">
                        <ScenarioDefinitionButton scenarioKey={scenario.key} revision={scenario.revision} yaml={scenario.definitionYaml} />
                    </span>
                </div>

                {scenario.samples.map((sample) => (
                    <div key={sample.sample} className="flex flex-col gap-2.5">
                        {sample.status === "Failed" && (
                            <Alert variant="destructive">
                                <p className="font-semibold">{`Sample ${sample.sample}: the agent returned nothing`}</p>
                                <p>{sample.error}</p>
                            </Alert>
                        )}
                        {sample.status === "NotSimulated" && (
                            <Alert variant="warning">
                                <p className="font-semibold">{`Sample ${sample.sample}: the simulated user could not be played`}</p>
                                <p>{sample.error} Not the agent's doing: run it again.</p>
                            </Alert>
                        )}
                        {(sample.undeclared?.length ?? 0) > 0 && (
                            <Alert variant="warning">
                                <p className="font-semibold">{`Sample ${sample.sample}: the agent returned what its version does not declare`}</p>
                                <p>{sample.undeclared?.join(", ")}</p>
                            </Alert>
                        )}
                    </div>
                ))}

                {scenario.checks.length === 0 && <p className="text-muted-foreground">No checks.</p>}
                {visibleChecks(scenario, hidePassed).map((check) => {
                    const unstableCheck = findUnstable(unstable, versionId, scenario.key, check.key, check.revision);
                    return (
                        <div key={check.key} id={checkAnchor(scenario.key, check.key)} className="check-target flex flex-col gap-1">
                            <div className="flex items-start justify-between gap-4">
                                <div className="flex flex-wrap items-center gap-2.5">
                                    <VerdictBadge verdict={check.passed ? "passed" : "failed"} />
                                    {check.regression != null && check.regression.state !== "Passed" && (
                                        <StateBadge state={check.regression.state}>{stateLabel(check.regression.state)}</StateBadge>
                                    )}
                                    {failedOnOtherData(check) && <DataChangedBadge />}
                                    <CheckHistory check={check} />
                                    {notJudgedCount(check) > 0 && (
                                        <Badge variant="warning">
                                            {`not judged: ${notJudgedCount(check)} of ${check.samples.length} samples`}
                                        </Badge>
                                    )}
                                    <p className="wrap-key font-semibold">{check.key}</p>
                                    <p className="text-muted-foreground">{checkSummary(check)}</p>
                                </div>
                                <div className="flex items-center gap-1">
                                    {check.judgedByModel && (
                                        <OnPaper text="model-judged">
                                            <ModelJudgedIcon model={check.judgeModel} />
                                        </OnPaper>
                                    )}
                                    {unstableCheck !== undefined && (
                                        <OnPaper text="unstable">
                                            <UnstableCheckIcon label={unstableTooltip(unstableCheck)} />
                                        </OnPaper>
                                    )}
                                    <span className="no-print">
                                        <CheckDefinitionButton
                                            definition={check.definition}
                                            revision={check.revision}
                                            latestRevision={check.latestRevision}
                                        />
                                    </span>
                                </div>
                            </div>
                            <ul className="text-sm">
                                {check.samples
                                    .filter((sample) => !sample.passed || check.samples.length === 1)
                                    .map((sample) => (
                                        <li key={sample.sample}>
                                            {check.samples.length > 1 ? `sample ${sample.sample}: ` : ""}
                                            <VerdictMessage message={sample.message} byModel={check.judgedByModel && !sample.notJudged} />
                                        </li>
                                    ))}
                            </ul>
                        </div>
                    );
                })}

                <div className="flex flex-col gap-2.5">
                    {scenario.samples.map((sample) => {
                        const id = String(sample.sample);
                        return (
                            <details
                                key={sample.sample}
                                className="sample rounded-md border border-border"
                                open={openSamples.includes(id)}
                                onToggle={(event) => {
                                    const open = event.currentTarget.open;
                                    setOpenSamples((current) =>
                                        open === current.includes(id)
                                            ? current
                                            : open
                                              ? [...current, id]
                                              : current.filter((item) => item !== id),
                                    );
                                }}
                            >
                                <summary className="sample-toggle cursor-pointer p-3">
                                    <SampleRowLabel scenario={scenario} sample={sample} />
                                </summary>
                                <div className="border-t border-border p-3">
                                    <div className="print-only mb-2.5 font-semibold">
                                        <SampleRowLabel scenario={scenario} sample={sample} />
                                    </div>
                                    <ConversationView
                                        items={sample.output ?? []}
                                        highlight={failedEvidenceKeys(scenario, sample.sample)}
                                        simulated={isSimulated(sample)}
                                    />
                                </div>
                            </details>
                        );
                    })}
                </div>
            </div>
        </Paper>
    );
}
