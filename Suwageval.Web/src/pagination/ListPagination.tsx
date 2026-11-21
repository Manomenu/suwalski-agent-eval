import { Paginated } from "@maniumek/suwfrontkit";

import { clampPage, DEFAULT_PAGE_SIZE, pageCountOf } from "./pages";

export function ListPagination({
    totalCount,
    page,
    pageSize = DEFAULT_PAGE_SIZE,
    onChange,
}: {
    totalCount: number;
    page: number;
    pageSize?: number;
    onChange: (page: number) => void;
}) {
    const pages = pageCountOf(totalCount, pageSize);
    if (pages === 1) return null;
    return (
        <Paginated paging={{ page: clampPage(page, totalCount, pageSize), pages, asking: false, failure: null, onMove: onChange }}>
            {null}
        </Paginated>
    );
}
