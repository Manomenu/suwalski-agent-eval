import { VisuallyHidden } from "@mantine/core";

import { CHANGE_TEXT, type Change } from "./compare";

/** The words a screen reader gets in place of a cell's colour against the baseline; nothing for a cell without a change. */
export function ChangeNote({ change }: { change: Change | null | undefined }) {
    return change == null ? null : <VisuallyHidden>{CHANGE_TEXT[change]}</VisuallyHidden>;
}
