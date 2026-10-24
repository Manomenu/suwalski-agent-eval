import { useState } from "react";

import { archivedFromSearch, searchWithArchived } from "./archivedParam";

/** "Show archived" of a list, kept in query parameter `param` so the view can be shared; changing it does not reload the page. */
export function useArchivedParam(param: string): [boolean, (show: boolean) => void] {
    const [show, setShow] = useState(() => archivedFromSearch(window.location.search, param));
    const select = (next: boolean) => {
        setShow(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithArchived(search, param, next)}${hash}`);
    };
    return [show, select];
}
