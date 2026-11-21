import { CHANGE_TEXT, type Change } from "./compare";

export function ChangeNote({ change }: { change: Change | null | undefined }) {
    return change == null ? null : <span className="sr-only">{CHANGE_TEXT[change]}</span>;
}
