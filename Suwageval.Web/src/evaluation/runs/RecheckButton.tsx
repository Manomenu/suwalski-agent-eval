import { Alert, Button, Diff, Dialog, Spinner, Tooltip } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";

import { Failure } from "../../api/Failure";
import { definitionYaml } from "../checks/definition";
import { ModelJudgedIcon } from "../../verdict/ModelJudgedIcon";
import { recheck, recheckPlanQuery, type ChangedCheck, type RunSummary } from "./api";
import { changesByScenario, judgedNote, planHeadline, recheckDisabledReason, recheckTooltip } from "./recheck";
import { runHref } from "../../routes/links";

export function RecheckButton({ run }: { run: Pick<RunSummary, "id" | "status"> }) {
    const [asking, setAsking] = useState(false);
    const disabledReason = recheckDisabledReason(run.status);
    return (
        <>
            <Tooltip label={recheckTooltip(run.status)}>
                <Button
                    variant="outline"
                    disabled={disabledReason !== null}
                    onClick={() => {
                        setAsking(true);
                    }}
                >
                    Re-check
                </Button>
            </Tooltip>
            {asking && (
                <RecheckDialog
                    runId={run.id}
                    onClose={() => {
                        setAsking(false);
                    }}
                />
            )}
        </>
    );
}

function RecheckDialog({ runId, onClose }: { runId: string; onClose: () => void }) {
    const state = useQuery({ ...recheckPlanQuery(runId), gcTime: 0 });
    const [busy, setBusy] = useState(false);
    const [failed, setFailed] = useState<{ error: unknown } | null>(null);

    const start = () => {
        setBusy(true);
        setFailed(null);
        recheck(runId).then(
            (created) => {
                window.location.assign(runHref(created.runId));
            },
            (failure: unknown) => {
                setFailed({ error: failure });
                setBusy(false);
            },
        );
    };

    const close = (
        <Button variant="outline" disabled={busy} onClick={onClose}>
            Close
        </Button>
    );
    const onOpenChange = (open: boolean) => {
        if (!open && !busy) onClose();
    };

    if (state.isPending) {
        return (
            <Dialog open onOpenChange={onOpenChange} title="Re-check this run?" width="wide">
                <div className="flex justify-center p-4">
                    <Spinner label="Loading the re-check plan" />
                </div>
            </Dialog>
        );
    }
    if (state.isError) {
        return (
            <Dialog open onOpenChange={onOpenChange} title="Re-check this run?" width="wide" actions={close}>
                <Alert variant="destructive">
                    <p className="font-semibold">The plan could not be loaded</p>
                    <Failure error={state.error} />
                </Alert>
            </Dialog>
        );
    }

    const plan = state.data;
    const judged = judgedNote(plan);
    return (
        <Dialog
            open
            onOpenChange={onOpenChange}
            title="Re-check this run?"
            width="wide"
            actions={
                <>
                    {close}
                    {plan.possible && (
                        <Button onClick={start} disabled={busy}>
                            {busy ? "Starting…" : "Start re-check"}
                        </Button>
                    )}
                </>
            }
        >
            <div className="flex flex-col gap-4">
                {plan.possible ? (
                    <>
                        <p>{planHeadline(plan)}</p>
                        {judged && <p className="font-medium">{judged}</p>}
                        <p className="text-sm text-muted-foreground">
                            The agent is not called: the answers stored in this run are judged again, and the turn times stay the run's.
                        </p>
                    </>
                ) : (
                    <Alert variant="warning">
                        <p className="font-semibold">Nothing to re-check</p>
                        <p>{plan.reason}</p>
                    </Alert>
                )}
                {plan.mismatches.length > 0 && (
                    <ul className="list-disc pl-5 text-sm" aria-label="Mismatches">
                        {plan.mismatches.map((mismatch) => (
                            <li key={mismatch}>{mismatch}</li>
                        ))}
                    </ul>
                )}
                {changesByScenario(plan.changedChecks).map((group) => (
                    <div key={group.scenarioKey} className="flex flex-col gap-1">
                        <p className="wrap-key font-semibold">{group.scenarioKey}</p>
                        {group.checks.map((check) => (
                            <ChangedCheckRow key={check.checkKey} check={check} judgeModel={plan.judgeModel} />
                        ))}
                    </div>
                ))}
                {plan.skippedScenarios.length > 0 && (
                    <div className="flex flex-col gap-1">
                        <p className="font-semibold">Skipped scenarios</p>
                        <ul className="list-disc pl-5 text-sm">
                            {plan.skippedScenarios.map((skipped) => (
                                <li key={skipped.key}>
                                    {skipped.key}: {skipped.reason}
                                </li>
                            ))}
                        </ul>
                    </div>
                )}
                {failed !== null && (
                    <Alert variant="destructive">
                        <p className="font-semibold">The re-check could not be started</p>
                        <Failure error={failed.error} />
                    </Alert>
                )}
            </div>
        </Dialog>
    );
}

function ChangedCheckRow({ check, judgeModel }: { check: ChangedCheck; judgeModel: string | null | undefined }) {
    const judgedNow = check.afterJudgedByModel;
    return (
        <div className="flex flex-col gap-1">
            <div className="flex flex-wrap items-center gap-2.5">
                <span className="wrap-key">{check.checkKey}</span>
                <span className="text-muted-foreground" aria-label={`${check.checkKey} change`}>
                    {check.change}
                </span>
                {judgedNow && <ModelJudgedIcon model={judgeModel} />}
            </div>
            <details className="rounded-md border border-border">
                <summary className="cursor-pointer p-2.5" aria-label={`Show change of ${check.checkKey}`}>
                    Show change
                </summary>
                <div className="border-t border-border p-2.5">
                    <Diff
                        before={check.before == null ? "" : definitionYaml(check.before)}
                        after={check.after == null ? "" : definitionYaml(check.after)}
                    />
                </div>
            </details>
        </div>
    );
}
