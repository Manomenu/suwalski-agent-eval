import { Button, Group, Stack, Text, Textarea } from "@mantine/core";
import { useEffect, useRef, useState } from "react";

import { setRunNote } from "./api";
import { NOTE_MAX_LENGTH, normalizeNote, noteProblem } from "./note";

/** How long typing pauses before the note is saved in the background. */
const NOTE_SAVE_DELAY_MS = 800;

type SaveState = { kind: "saved" } | { kind: "saving" } | { kind: "failed"; message: string };

/**
 * The note of a run, in the header: shown as text (it prints), edited in a textarea behind "Edit note" / "Add note". Typing never waits for the
 * network: the note is saved a moment after the last keystroke, one request at a time, and a small status says where it stands. A failure is
 * shown with the server's words and the save is tried again with the next change.
 */
export function RunNote({ runId, initial }: { runId: string; initial: string | null | undefined }) {
    const [saved, setSaved] = useState(normalizeNote(initial));
    const [draft, setDraft] = useState(initial ?? "");
    const [editing, setEditing] = useState(false);
    const [state, setState] = useState<SaveState>({ kind: "saved" });
    const [failedFor, setFailedFor] = useState<string | null>(null);
    const inFlight = useRef(false);

    const wanted = normalizeNote(draft);
    const problem = noteProblem(draft);

    useEffect(() => {
        if (wanted === saved || problem !== null || inFlight.current || failedFor === draft) return;
        const timer = setTimeout(() => {
            inFlight.current = true;
            setState({ kind: "saving" });
            setRunNote(runId, wanted).then(
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
    }, [runId, wanted, saved, problem, draft, failedFor]);

    return (
        <Stack gap="xs" align="flex-start">
            {saved !== null && (
                <Text className="wrap-key" style={{ whiteSpace: "pre-wrap" }}>
                    Note: {saved}
                </Text>
            )}
            <Group gap="sm" className="no-print">
                <Button
                    size="compact-sm"
                    variant="light"
                    onClick={() => {
                        setEditing(!editing);
                    }}
                >
                    {editing ? "Close note" : saved === null ? "Add note" : "Edit note"}
                </Button>
                {editing && (
                    <Text size="sm" c={state.kind === "failed" ? "red" : "dimmed"} role="status">
                        {state.kind === "saving" && "Saving…"}
                        {state.kind === "saved" && (wanted === saved ? "Saved" : "Not saved yet")}
                        {state.kind === "failed" && `Could not save: ${state.message}`}
                    </Text>
                )}
            </Group>
            {editing && (
                <Textarea
                    className="no-print"
                    w="100%"
                    aria-label="Note"
                    placeholder="Why was this run started?"
                    autosize
                    minRows={2}
                    maxRows={8}
                    value={draft}
                    error={problem}
                    description={`${String(draft.trim().length)} / ${String(NOTE_MAX_LENGTH)}`}
                    onChange={(event) => {
                        setDraft(event.currentTarget.value);
                    }}
                />
            )}
        </Stack>
    );
}
