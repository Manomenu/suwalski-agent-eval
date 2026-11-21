import { Alert, Button, Textarea } from "@maniumek/suwfrontkit";
import { IconCheck, IconInfoCircle, IconNotes, IconPencil } from "@tabler/icons-react";
import { useEffect, useEffectEvent, useRef, useState } from "react";

import { NOTE_MAX_LENGTH, normalizeNote, noteProblem } from "./note";

const NOTE_SAVE_DELAY_MS = 800;

type SaveState = { kind: "saved" } | { kind: "saving" } | { kind: "failed"; message: string };

export function NoteBox({
    initial,
    save,
    placeholder,
}: {
    initial: string | null | undefined;
    save: (note: string | null) => Promise<void>;
    placeholder: string;
}) {
    const [saved, setSaved] = useState(normalizeNote(initial));
    const [draft, setDraft] = useState(initial ?? "");
    const [editing, setEditing] = useState(false);
    const [state, setState] = useState<SaveState>({ kind: "saved" });
    const [failedFor, setFailedFor] = useState<string | null>(null);
    const inFlight = useRef(false);

    const wanted = normalizeNote(draft);
    const problem = noteProblem(draft);

    const saveNow = useEffectEvent((note: string | null) => save(note));

    useEffect(() => {
        if (wanted === saved || problem !== null || inFlight.current || failedFor === draft) return;
        const timer = setTimeout(() => {
            inFlight.current = true;
            setState({ kind: "saving" });
            saveNow(wanted).then(
                () => {
                    inFlight.current = false;
                    setSaved(wanted);
                    setState({ kind: "saved" });
                },
                (failure: unknown) => {
                    inFlight.current = false;
                    setFailedFor(draft);
                    setState({ kind: "failed", message: failure instanceof Error ? failure.message : "The note could not be saved." });
                },
            );
        }, NOTE_SAVE_DELAY_MS);
        return () => {
            clearTimeout(timer);
        };
    }, [wanted, saved, problem, draft, failedFor]);

    if (!editing && saved === null) {
        return (
            <Button
                className="no-print w-fit"
                size="sm"
                variant="ghost"
                onClick={() => {
                    setEditing(true);
                }}
            >
                <IconNotes size={16} aria-hidden />
                Add note
            </Button>
        );
    }

    return (
        <Alert className="note-box" role="note">
            <div className="flex items-start gap-3">
                <IconInfoCircle size={18} aria-hidden className="mt-0.5 shrink-0" />
                <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                    <p className="font-semibold">Note</p>
                    {editing ? (
                        <>
                            <Textarea
                                aria-label="Note"
                                placeholder={placeholder}
                                autoFocus
                                rows={3}
                                value={draft}
                                aria-invalid={problem !== null}
                                onChange={(event) => {
                                    setDraft(event.currentTarget.value);
                                }}
                                onKeyDown={(event) => {
                                    if (event.key === "Escape") setEditing(false);
                                }}
                            />
                            {problem !== null && <p className="text-sm text-destructive">{problem}</p>}
                            <div className="flex flex-wrap items-center justify-between gap-3">
                                <div className="flex flex-wrap items-center gap-1.5">
                                    <p
                                        className={`text-xs ${state.kind === "failed" ? "text-destructive" : "text-muted-foreground"}`}
                                        role="status"
                                    >
                                        {state.kind === "saving" && "Saving…"}
                                        {state.kind === "saved" && (wanted === saved ? "Saved" : "Not saved yet")}
                                        {state.kind === "failed" && `Could not save: ${state.message}`}
                                    </p>
                                    <p className="text-xs text-muted-foreground">
                                        · {String(draft.trim().length)} / {String(NOTE_MAX_LENGTH)}
                                    </p>
                                </div>
                                <Button
                                    size="sm"
                                    variant="outline"
                                    onClick={() => {
                                        setEditing(false);
                                    }}
                                >
                                    <IconCheck size={14} aria-hidden />
                                    Done
                                </Button>
                            </div>
                        </>
                    ) : (
                        <div className="flex items-start justify-between gap-3">
                            <p className="wrap-key text-sm whitespace-pre-wrap">{saved}</p>
                            <Button
                                className="no-print"
                                variant="ghost"
                                size="icon"
                                aria-label="Edit note"
                                onClick={() => {
                                    setEditing(true);
                                }}
                            >
                                <IconPencil size={16} aria-hidden />
                            </Button>
                        </div>
                    )}
                </div>
            </div>
        </Alert>
    );
}
