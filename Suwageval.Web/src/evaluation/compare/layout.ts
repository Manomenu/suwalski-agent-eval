export const LABEL_COLUMN_WIDTH = 220;
export const COLUMN_MIN_WIDTH = 190;

export const tableMinWidth = (columns: number) => LABEL_COLUMN_WIDTH + columns * COLUMN_MIN_WIDTH;

export const PRINT_LABEL_COLUMN_WIDTH = 140;
export const PRINT_COLUMN_WIDTH = 125;

export const PRINT_PAGE_WIDTH = Math.floor(((297 - 2 * 10) / 25.4) * 96);

export const PRINT_MAX_RUNS = 7;

export const COMPARE_SCROLL = "compare-scroll w-full overflow-x-auto";

export const COMPARE_TABLE =
    "compare-table w-full table-fixed border-collapse text-sm [&_tbody_tr:hover]:bg-accent/40 [&_td]:border-b [&_td]:px-2.5 [&_td]:py-[7px] [&_th]:border-b [&_th]:px-2.5 [&_th]:py-[7px] [&_th]:text-left [&_th]:font-bold";
