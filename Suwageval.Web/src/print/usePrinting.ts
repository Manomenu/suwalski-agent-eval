import { useSyncExternalStore } from "react";

const subscribe = (notify: () => void) => {
    const media = window.matchMedia("print");
    media.addEventListener("change", notify);
    return () => {
        media.removeEventListener("change", notify);
    };
};
const printing = () => window.matchMedia("print").matches;

/**
 * Whether the page is being laid out for paper (the print dialog is open, or print media is emulated). For what CSS cannot do: a part
 * that is not rendered while folded is rendered for paper. Synchronous (useSyncExternalStore), so the paper gets it before it is laid out.
 */
export function usePrinting(): boolean {
    return useSyncExternalStore(subscribe, printing);
}
