export function percentValue(share: number): number {
    return Math.round(share * 1000) / 10;
}

export function percent(share: number): string {
    return `${percentValue(share)}%`;
}

export function seconds(milliseconds: number): number {
    return Math.round(milliseconds / 100) / 10;
}

export function formatSeconds(milliseconds: number): string {
    return `${seconds(milliseconds).toFixed(1)} s`;
}
