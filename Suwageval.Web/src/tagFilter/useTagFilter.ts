import { useState } from "react";

import { searchWithTags, selectedTagsOf, TAG_PARAM } from "./tagSelection";

/** The selected tags, kept in the address (`&tag=a&tag=b`, or another parameter: a page with two tag fields) so the view can be shared; changing them does not reload the page. */
export function useTagFilter(param = TAG_PARAM): [string[], (tags: string[]) => void] {
    const [selected, setSelected] = useState(() => selectedTagsOf(window.location.search, param));
    const select = (tags: string[]) => {
        setSelected(tags);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithTags(search, tags, param)}${hash}`);
    };
    return [selected, select];
}
