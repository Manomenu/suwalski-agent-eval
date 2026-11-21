export function percentValue(share: number): number {
    return Math.round(share * 1000) / 10;
}

export function seconds(milliseconds: number): number {
    return Math.round(milliseconds / 100) / 10;
}
