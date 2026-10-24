import { useState } from "react";

import { openScenariosOf, searchWithOpenScenarios, toggledScenario } from "./openScenarios";

/** The expanded scenario rows, kept in the address so the view can be linked; toggling one does not reload the page. */
export function useOpenScenarios(): [readonly string[], (key: string) => void] {
    const [open, setOpen] = useState(() => openScenariosOf(window.location.search));
    const toggle = (key: string) => {
        const next = toggledScenario(open, key);
        setOpen(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithOpenScenarios(search, next)}${hash}`);
    };
    return [open, toggle];
}
