import { Pagination } from "@mantine/core";

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
    const pageCount = pageCountOf(totalCount, pageSize);
    if (pageCount === 1) return null;
    return <Pagination total={pageCount} value={clampPage(page, totalCount, pageSize)} onChange={onChange} />;
}
