// Numbers that shape the comparison table: the label column (scenario and tag names) and one column per run.
export const LABEL_COLUMN_WIDTH = 220;
export const COLUMN_MIN_WIDTH = 190;

/** The width under which the table scrolls sideways instead of squeezing its columns. */
export const tableMinWidth = (columns: number) => LABEL_COLUMN_WIDTH + columns * COLUMN_MIN_WIDTH;

// On paper (print.css) the columns are narrower and the font smaller, so that PRINT_MAX_RUNS runs fit one A4 landscape page.
export const PRINT_LABEL_COLUMN_WIDTH = 140;
export const PRINT_COLUMN_WIDTH = 125;
/** A4 landscape (297 mm) less the 10 mm margins of the `@page` rule in print.css, at 96 dpi (CSS pixels). */
export const PRINT_PAGE_WIDTH = Math.floor(((297 - 2 * 10) / 25.4) * 96);
/**
 * How many runs a comparison holds, printed or not: what `printMaxRuns` gives for the widths above (a test checks it). A literal, because
 * the server and the CLI keep the same number (`COMPARE_MAX_RUNS` in routes/links.ts is the web's other copy) and a server test reads this line.
 */
export const PRINT_MAX_RUNS = 7;
