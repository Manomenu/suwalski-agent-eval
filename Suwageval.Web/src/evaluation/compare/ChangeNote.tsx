import { VisuallyHidden } from "@mantine/core";

import { CHANGE_TEXT, type Change } from "./compare";

export function ChangeNote({ change }: { change: Change | null | undefined }) {
    return change == null ? null : <VisuallyHidden>{CHANGE_TEXT[change]}</VisuallyHidden>;
}
