// The note on a run: why it was started. The rules are the server's (`RunNoteRules`): trimmed, at most 500 characters, blank means none.

export const NOTE_MAX_LENGTH = 500;

/** The note as the server stores it: trimmed, and none when blank. */
export function normalizeNote(note: string | null | undefined): string | null {
    const trimmed = note?.trim() ?? "";
    return trimmed === "" ? null : trimmed;
}

/** What is wrong with the note, or null when it can be saved. */
export function noteProblem(note: string): string | null {
    const normalized = normalizeNote(note);
    return normalized !== null && normalized.length > NOTE_MAX_LENGTH ? `The note is at most ${String(NOTE_MAX_LENGTH)} characters.` : null;
}
