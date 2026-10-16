// Numbers that shape the compare table: the label column (scenario and check keys) and one column per run, five at most.
export const LABEL_COLUMN_WIDTH = 220;
export const COLUMN_MIN_WIDTH = 150;

/** The width under which the table scrolls sideways instead of squeezing its columns. */
export const tableMinWidth = (columns: number) => LABEL_COLUMN_WIDTH + columns * COLUMN_MIN_WIDTH;
