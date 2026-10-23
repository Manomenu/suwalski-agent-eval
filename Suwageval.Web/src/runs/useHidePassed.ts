import { useState } from "react";

import { hidePassedOf, searchWithHidePassed } from "./hidePassed";

/** Whether passed checks are hidden, kept in the address (`&hide=passed`) so the view can be shared; changing it does not reload the page. */
export function useHidePassed(): [boolean, (hide: boolean) => void] {
    const [hidden, setHidden] = useState(() => hidePassedOf(window.location.search));
    const change = (hide: boolean) => {
        setHidden(hide);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithHidePassed(search, hide)}${hash}`);
    };
    return [hidden, change];
}
