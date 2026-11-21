import { Alert, Button, CopyButton, Dialog, Input, Label, Select, Spinner, ToggleChip } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";
import { useId, useState } from "react";

import { Failure } from "../../api/Failure";
import { scenarioDraftQuery } from "./api";
import type { ConversationItem } from "../../conversation/items";
import {
    DRAFT_CUTS,
    draftCutOf,
    draftTurns,
    MAX_DRAFT_TURNS,
    messageOptions,
    NOTHING_TO_SIMULATE,
    SCENARIO_DRAFT_HELP,
    SIMULATE_REST_HELP,
    type DraftCut,
} from "./conversations";

export function ScenarioDraftButton({
    conversationId,
    items,
    failed,
}: {
    conversationId: string;
    items: ConversationItem[];
    failed: boolean;
}) {
    const cutId = useId();
    const messageId = useId();
    const turnsId = useId();
    const [opened, setOpened] = useState(false);
    const [cut, setCut] = useState<DraftCut>("end");
    const [ordinal, setOrdinal] = useState<string | null>(null);
    const [simulate, setSimulate] = useState(false);
    const [turns, setTurns] = useState<number | null>(null);
    const [generated, setGenerated] = useState<number | null>(null);
    const options = cut === "end" ? [] : messageOptions(items, cut);
    const picked = ordinal ?? options.at(-1)?.value ?? "1";
    const turnsLeft = cut === "end" ? 0 : draftTurns(items, cut, Number(picked));
    const simulating = simulate && turnsLeft > 0;
    const shownTurns = turns ?? turnsLeft;
    const changed = () => {
        setGenerated(null);
    };
    return (
        <>
            <Button
                variant={failed ? "destructive" : "outline"}
                onClick={() => {
                    setOpened(true);
                }}
            >
                Scenario YAML
            </Button>
            <Dialog
                open={opened}
                onOpenChange={(open) => {
                    if (!open) setOpened(false);
                }}
                title="This conversation as a scenario"
                width="wide"
            >
                {opened && (
                    <div className="flex flex-col gap-3">
                        <div className="flex flex-col gap-1">
                            <Label htmlFor={cutId}>Copy</Label>
                            <Select
                                id={cutId}
                                value={cut}
                                onChange={(event) => {
                                    setCut(draftCutOf(event.target.value));
                                    setOrdinal(null);
                                    setTurns(null);
                                    changed();
                                }}
                            >
                                {DRAFT_CUTS.map((option) => (
                                    <option key={option.value} value={option.value}>
                                        {option.label}
                                    </option>
                                ))}
                            </Select>
                        </div>
                        {cut !== "end" && (
                            <div className="flex flex-col gap-1">
                                <Label htmlFor={messageId}>{cut === "user" ? "Last user message" : "Last agent message"}</Label>
                                <Select
                                    id={messageId}
                                    value={picked}
                                    onChange={(event) => {
                                        setOrdinal(event.target.value);
                                        setTurns(null);
                                        changed();
                                    }}
                                >
                                    {options.map((option) => (
                                        <option key={option.value} value={option.value}>
                                            {option.label}
                                        </option>
                                    ))}
                                </Select>
                            </div>
                        )}
                        {cut !== "end" && (
                            <div className="flex flex-wrap items-end gap-3">
                                <ToggleChip
                                    pressed={simulating}
                                    disabled={turnsLeft === 0}
                                    help={turnsLeft === 0 ? NOTHING_TO_SIMULATE : SIMULATE_REST_HELP}
                                    onPressedChange={(pressed) => {
                                        setSimulate(pressed);
                                        changed();
                                    }}
                                >
                                    Simulate the rest
                                </ToggleChip>
                                {simulating && (
                                    <>
                                        <div className="flex w-25 flex-col gap-1">
                                            <Label htmlFor={turnsId}>Turns</Label>
                                            <Input
                                                id={turnsId}
                                                type="number"
                                                min={1}
                                                max={MAX_DRAFT_TURNS}
                                                step={1}
                                                value={shownTurns}
                                                onChange={(event) => {
                                                    const value = Math.trunc(event.target.valueAsNumber);
                                                    setTurns(Number.isNaN(value) ? null : Math.min(Math.max(value, 1), MAX_DRAFT_TURNS));
                                                    changed();
                                                }}
                                            />
                                        </div>
                                        <Button
                                            onClick={() => {
                                                setGenerated(Date.now());
                                            }}
                                        >
                                            Generate scenario
                                        </Button>
                                    </>
                                )}
                            </div>
                        )}
                        {!simulating && <Draft conversationId={conversationId} cut={cut} ordinal={Number(picked)} turns={null} run={0} />}
                        {simulating && generated !== null && (
                            <Draft conversationId={conversationId} cut={cut} ordinal={Number(picked)} turns={shownTurns} run={generated} />
                        )}
                    </div>
                )}
            </Dialog>
        </>
    );
}

function Draft({
    conversationId,
    cut,
    ordinal,
    turns,
    run,
}: {
    conversationId: string;
    cut: DraftCut;
    ordinal: number;
    turns: number | null;
    run: number;
}) {
    const draft = useQuery(scenarioDraftQuery(conversationId, cut, ordinal, turns, run));
    if (draft.isPending) return <Spinner label="Loading the draft" />;
    if (draft.isError) return <Failure className="text-destructive" error={draft.error} />;
    const { path, yaml, notice } = draft.data;
    return (
        <div className="flex flex-col gap-3">
            <p className="text-sm">{SCENARIO_DRAFT_HELP}</p>
            {notice && <Alert variant="warning">{notice}</Alert>}
            <div className="flex items-center gap-2.5">
                <code aria-label="File path" className="rounded-sm bg-secondary px-1 font-mono text-[13px]">
                    {path}
                </code>
                <CopyButton value={path} label="Copy the path" variant="ghost" size="xs" />
            </div>
            <div className="flex justify-end">
                <CopyButton value={yaml} label="Copy the YAML" variant="ghost" size="xs" />
            </div>
            <pre aria-label="Scenario YAML" className="overflow-x-auto rounded-md bg-secondary p-3 font-mono text-[13px]">
                {yaml}
            </pre>
        </div>
    );
}
