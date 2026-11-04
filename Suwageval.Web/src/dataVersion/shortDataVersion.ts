const SHORT_MAX = 12;
const SHORT_LENGTH = 8;

export function shortDataVersion(value: string): string {
    return value.length <= SHORT_MAX ? value : `${value.slice(0, SHORT_LENGTH)}…`;
}
