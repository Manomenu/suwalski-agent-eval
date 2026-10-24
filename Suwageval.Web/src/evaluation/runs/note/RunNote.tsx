import { NoteBox } from "../../../note/NoteBox";
import { setRunNote } from "../api";

/** The note of a run, in the header of its page: why it was started. */
export function RunNote({ runId, initial }: { runId: string; initial: string | null | undefined }) {
    return <NoteBox key={runId} initial={initial} save={(note) => setRunNote(runId, note)} placeholder="Why was this run started?" />;
}
