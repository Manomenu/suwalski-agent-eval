export const LABEL_COLUMN_WIDTH = 220;
export const COLUMN_MIN_WIDTH = 190;

export const tableMinWidth = (columns: number) => LABEL_COLUMN_WIDTH + columns * COLUMN_MIN_WIDTH;

export const PRINT_LABEL_COLUMN_WIDTH = 140;
export const PRINT_COLUMN_WIDTH = 125;

export const PRINT_PAGE_WIDTH = Math.floor(((297 - 2 * 10) / 25.4) * 96);

export const PRINT_MAX_RUNS = 7;
