import { useState } from "react";

import { hidePassedOf, searchWithHidePassed } from "./hidePassed";

export function useHidePassed(): [boolean, (hide: boolean) => void] {
    const [hidden, setHidden] = useState(() => hidePassedOf(window.location.search));
    const change = (hide: boolean) => {
        setHidden(hide);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithHidePassed(search, hide)}${hash}`);
    };
    return [hidden, change];
}
