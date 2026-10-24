import { Accordion, Alert, Button, Group, List, Loader, Modal, Stack, Text, Tooltip } from "@mantine/core";
import { useState } from "react";

import { useRequest } from "../../api/useRequest";
import { definitionYaml } from "../checks/definition";
import { ModelJudgedIcon } from "../checks/ModelJudgedIcon";
import { diffBlocks, diffRows } from "../../diff/lineDiff";
import { LineDiffView } from "../../diff/LineDiffView";
import { fetchRecheckPlan, recheck, type ChangedCheck, type RunSummary } from "./api";
import { changesByScenario, judgedNote, planHeadline, recheckDisabledReason, recheckTooltip } from "./recheck";
import { runHref } from "../../routes/links";

const RECHECK_TOOLTIP_WIDTH = 340;

/**
 * "Re-check": judges this run's stored answers again with the current checks — the agent is not asked. Enabled for a finished run;
 * a click opens a dialog that fetches what would be evaluated again, and only then offers "Start re-check".
 */
export function RecheckButton({ run }: { run: Pick<RunSummary, "id" | "status"> }) {
    const [asking, setAsking] = useState(false);
    const disabledReason = recheckDisabledReason(run.status);
    return (
        <>
            <Tooltip label={recheckTooltip(run.status)} multiline w={RECHECK_TOOLTIP_WIDTH}>
                <Button
                    variant="default"
                    disabled={disabledReason !== null}
                    onClick={() => {
                        setAsking(true);
                    }}
                >
                    Re-check
                </Button>
            </Tooltip>
            <Modal
                opened={asking}
                onClose={() => {
                    setAsking(false);
                }}
                title="Re-check this run?"
                size="lg"
            >
                <RecheckDialog
                    runId={run.id}
                    onClose={() => {
                        setAsking(false);
                    }}
                />
            </Modal>
        </>
    );
}

/** Mounted only while the dialog is open: it asks for the plan when it opens and abandons the request when it closes. */
function RecheckDialog({ runId, onClose }: { runId: string; onClose: () => void }) {
    const state = useRequest(runId, (signal) => fetchRecheckPlan(runId, signal));
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const start = () => {
        setBusy(true);
        setError(null);
        recheck(runId).then(
            (created) => {
                window.location.assign(runHref(created.runId));
            },
            (failure: unknown) => {
                setError(failure instanceof Error ? failure.message : "The re-check could not be started.");
                setBusy(false);
            },
        );
    };

    if (state.kind === "loading") {
        return (
            <Stack align="center" p="md">
                <Loader aria-label="Loading the re-check plan" />
            </Stack>
        );
    }
    if (state.kind === "error") {
        return (
            <Stack>
                <Alert color="red" title="The plan could not be loaded">
                    {state.error.message}
                </Alert>
                <CloseRow onClose={onClose} />
            </Stack>
        );
    }

    const plan = state.value;
    const judged = judgedNote(plan);
    return (
        <Stack>
            {plan.possible ? (
                <>
                    <Text>{planHeadline(plan)}</Text>
                    {judged && <Text fw={500}>{judged}</Text>}
                    <Text size="sm" c="dimmed">
                        The agent is not called: the answers stored in this run are judged again, and the turn times stay the run's.
                    </Text>
                </>
            ) : (
                <Alert color="yellow" title="Nothing to re-check">
                    {plan.reason}
                </Alert>
            )}
            {plan.mismatches.length > 0 && (
                <List size="sm" aria-label="Mismatches">
                    {plan.mismatches.map((mismatch) => (
                        <List.Item key={mismatch}>{mismatch}</List.Item>
                    ))}
                </List>
            )}
            {changesByScenario(plan.changedChecks).map((group) => (
                <Stack key={group.scenarioKey} gap={4}>
                    <Text fw={600} className="wrap-key">
                        {group.scenarioKey}
                    </Text>
                    {group.checks.map((check) => (
                        <ChangedCheckRow key={check.checkKey} check={check} judgeModel={plan.judgeModel} />
                    ))}
                </Stack>
            ))}
            {plan.skippedScenarios.length > 0 && (
                <Stack gap={4}>
                    <Text fw={600}>Skipped scenarios</Text>
                    <List size="sm">
                        {plan.skippedScenarios.map((skipped) => (
                            <List.Item key={skipped.key}>
                                {skipped.key}: {skipped.reason}
                            </List.Item>
                        ))}
                    </List>
                </Stack>
            )}
            {error && (
                <Alert color="red" title="The re-check could not be started">
                    {error}
                </Alert>
            )}
            <CloseRow onClose={onClose} busy={busy}>
                {plan.possible && (
                    <Button onClick={start} loading={busy} disabled={busy}>
                        Start re-check
                    </Button>
                )}
            </CloseRow>
        </Stack>
    );
}

function CloseRow({ onClose, busy = false, children }: { onClose: () => void; busy?: boolean; children?: React.ReactNode }) {
    return (
        <Group justify="flex-end">
            <Button variant="default" disabled={busy} onClick={onClose}>
                Close
            </Button>
            {children}
        </Group>
    );
}

/** One check of the plan with how it changed; a changed check can show the definition the run used next to the current one. */
function ChangedCheckRow({ check, judgeModel }: { check: ChangedCheck; judgeModel: string | null | undefined }) {
    const judgedNow = check.afterJudgedByModel;
    return (
        <Stack gap={4}>
            <Group gap="xs">
                <Text span className="wrap-key">
                    {check.checkKey}
                </Text>
                <Text span c="dimmed" aria-label={`${check.checkKey} change`}>
                    {check.change}
                </Text>
                {judgedNow && <ModelJudgedIcon model={judgeModel} />}
            </Group>
            <Accordion variant="contained">
                <Accordion.Item value="change">
                    <Accordion.Control aria-label={`Show change of ${check.checkKey}`}>Show change</Accordion.Control>
                    <Accordion.Panel>
                        {/* The check as the run used it against the check now: an added one is all new lines, a removed one all gone. */}
                        <LineDiffView
                            blocks={diffBlocks(
                                diffRows(
                                    check.before == null ? "" : definitionYaml(check.before),
                                    check.after == null ? "" : definitionYaml(check.after),
                                ),
                            )}
                        />
                    </Accordion.Panel>
                </Accordion.Item>
            </Accordion>
        </Stack>
    );
}
