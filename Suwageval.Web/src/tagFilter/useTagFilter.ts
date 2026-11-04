import { useState } from "react";

import { searchWithTags, selectedTagsOf, TAG_PARAM } from "./tagSelection";

export function useTagFilter(param = TAG_PARAM): [string[], (tags: string[]) => void] {
    const [selected, setSelected] = useState(() => selectedTagsOf(window.location.search, param));
    const select = (tags: string[]) => {
        setSelected(tags);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithTags(search, tags, param)}${hash}`);
    };
    return [selected, select];
}
