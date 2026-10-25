import { useState } from "react";

import { environmentOf, searchWithEnvironment } from "./environmentFilter";

/** The Monitoring page's environment, kept in the address; changing it does not reload the page. */
export function useEnvironmentFilter(): [string | null, (environment: string | null) => void] {
    const [environment, setEnvironment] = useState(() => environmentOf(window.location.search));
    const select = (next: string | null) => {
        setEnvironment(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithEnvironment(search, next)}${hash}`);
    };
    return [environment, select];
}
