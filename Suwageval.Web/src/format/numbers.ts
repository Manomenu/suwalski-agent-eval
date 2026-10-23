// Shares and durations as the pages write them.

/** 0.666 → 66.7: a share as a percentage rounded to one decimal. */
export function percentValue(share: number): number {
    return Math.round(share * 1000) / 10;
}

/** 0.666 → "66.7%" (no trailing zeros, like the CLI report). */
export function percent(share: number): string {
    return `${percentValue(share)}%`;
}

/** Milliseconds as seconds rounded to one decimal: 9840 → 9.8. */
export function seconds(milliseconds: number): number {
    return Math.round(milliseconds / 100) / 10;
}

/** 9800 → "9.8 s" (one decimal). */
export function formatSeconds(milliseconds: number): string {
    return `${seconds(milliseconds).toFixed(1)} s`;
}
