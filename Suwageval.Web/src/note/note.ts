export const NOTE_MAX_LENGTH = 500;

export function normalizeNote(note: string | null | undefined): string | null {
    const trimmed = note?.trim() ?? "";
    return trimmed === "" ? null : trimmed;
}

export function noteProblem(note: string): string | null {
    const normalized = normalizeNote(note);
    return normalized !== null && normalized.length > NOTE_MAX_LENGTH ? `The note is at most ${String(NOTE_MAX_LENGTH)} characters.` : null;
}
