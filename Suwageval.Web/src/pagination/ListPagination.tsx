import { PreviousNext } from "@maniumek/suwfrontkit";

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
    const shown = clampPage(page, totalCount, pageSize);
    return (
        <PreviousNext
            label="Pages"
            counter={`Page ${String(shown)} of ${String(pages)}`}
            previousDisabled={shown <= 1}
            nextDisabled={shown >= pages}
            onPrevious={() => {
                onChange(shown - 1);
            }}
            onNext={() => {
                onChange(shown + 1);
            }}
            className="justify-center"
        />
    );
}
