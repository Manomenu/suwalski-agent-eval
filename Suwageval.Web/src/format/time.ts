/** An ISO time as the reader's local date and time; a dash for none. */
export function formatTime(iso: string | null | undefined): string {
    if (!iso) return "—";
    return new Date(iso).toLocaleString("en-GB");
}
