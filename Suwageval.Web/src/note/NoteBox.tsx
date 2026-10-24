import { ActionIcon, Alert, Button, Group, Stack, Text, Textarea, ThemeIcon } from "@mantine/core";
import { IconCheck, IconInfoCircle, IconNotes, IconPencil } from "@tabler/icons-react";
import { useEffect, useEffectEvent, useRef, useState } from "react";

import { NOTE_MAX_LENGTH, normalizeNote, noteProblem } from "./note";

/** How long typing pauses before the note is saved in the background. */
const NOTE_SAVE_DELAY_MS = 800;

type SaveState = { kind: "saved" } | { kind: "saving" } | { kind: "failed"; message: string };

/**
 * A note, in the header of a run or a comparison: an info box with the text (it prints), edited in that same box — the pencil turns the text into
 * a textarea in its place. Typing never waits for the network: the note is saved a moment after the last keystroke, one request at
 * a time, and a small status says where it stands. A failure is shown with the server's words and the save is tried again with the
 * next change. Without a note only an "Add note" button shows (not on paper).
 */
export function NoteBox({
    initial,
    save,
    placeholder,
}: {
    initial: string | null | undefined;
    /** Stores the note (null: none); rejects with an `Error` carrying the server's words. */
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

    // The latest save, without making the debounce restart whenever the parent renders a new callback.
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
                className="no-print"
                size="compact-sm"
                variant="subtle"
                w="fit-content"
                leftSection={<IconNotes size={16} aria-hidden />}
                onClick={() => {
                    setEditing(true);
                }}
            >
                Add note
            </Button>
        );
    }

    return (
        <Alert
            className="note-box"
            variant="light"
            color="blue"
            radius="md"
            title="Note"
            icon={
                <ThemeIcon variant="light" color="blue" radius="xl" size="md">
                    <IconInfoCircle size={18} aria-hidden />
                </ThemeIcon>
            }
        >
            {editing ? (
                <Stack gap={6}>
                    <Textarea
                        aria-label="Note"
                        placeholder={placeholder}
                        autosize
                        autoFocus
                        minRows={2}
                        maxRows={8}
                        value={draft}
                        error={problem}
                        onChange={(event) => {
                            setDraft(event.currentTarget.value);
                        }}
                        onKeyDown={(event) => {
                            if (event.key === "Escape") setEditing(false);
                        }}
                    />
                    <Group justify="space-between" gap="sm">
                        <Group gap={6}>
                            <Text size="xs" c={state.kind === "failed" ? "red" : "dimmed"} role="status">
                                {state.kind === "saving" && "Saving…"}
                                {state.kind === "saved" && (wanted === saved ? "Saved" : "Not saved yet")}
                                {state.kind === "failed" && `Could not save: ${state.message}`}
                            </Text>
                            <Text size="xs" c="dimmed">
                                · {String(draft.trim().length)} / {String(NOTE_MAX_LENGTH)}
                            </Text>
                        </Group>
                        <Button
                            size="compact-sm"
                            variant="light"
                            color="blue"
                            leftSection={<IconCheck size={14} aria-hidden />}
                            onClick={() => {
                                setEditing(false);
                            }}
                        >
                            Done
                        </Button>
                    </Group>
                </Stack>
            ) : (
                <Group justify="space-between" align="flex-start" wrap="nowrap" gap="sm">
                    <Text size="sm" className="wrap-key" style={{ whiteSpace: "pre-wrap" }}>
                        {saved}
                    </Text>
                    <ActionIcon
                        className="no-print"
                        variant="subtle"
                        color="blue"
                        aria-label="Edit note"
                        onClick={() => {
                            setEditing(true);
                        }}
                    >
                        <IconPencil size={16} aria-hidden />
                    </ActionIcon>
                </Group>
            )}
        </Alert>
    );
}
