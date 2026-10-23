// How a retrieval data version is shown where space is short: a hash of 64 characters is told apart by its start.

/** Longer than this, a data version is shown by its start. */
const SHORT_MAX = 12;
const SHORT_LENGTH = 8;

/** A data version short enough for a table cell, a header or a filter: as it is, or its first eight characters and "…". */
export function shortDataVersion(value: string): string {
    return value.length <= SHORT_MAX ? value : `${value.slice(0, SHORT_LENGTH)}…`;
}
