// Numbers that shape the variants table: the label column (scenario and tag names) and one column per run.
export const LABEL_COLUMN_WIDTH = 220;
export const COLUMN_MIN_WIDTH = 170;

/** The width under which the table scrolls sideways instead of squeezing its columns. */
export const tableMinWidth = (columns: number) => LABEL_COLUMN_WIDTH + columns * COLUMN_MIN_WIDTH;
