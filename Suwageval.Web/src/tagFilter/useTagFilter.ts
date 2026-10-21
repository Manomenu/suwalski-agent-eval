import { useState } from "react";

import { searchWithTags, selectedTagsOf } from "./tagSelection";

/** The selected tags, kept in the address (`&tag=a&tag=b`) so the view can be shared; changing them does not reload the page. */
export function useTagFilter(): [string[], (tags: string[]) => void] {
    const [selected, setSelected] = useState(() => selectedTagsOf(window.location.search));
    const select = (tags: string[]) => {
        setSelected(tags);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithTags(search, tags)}${hash}`);
    };
    return [selected, select];
}
