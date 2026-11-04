export function shortId(id: string): string {
    return id.slice(0, 8);
}

export function versionTitle(id: string, label: string | null | undefined): string {
    return label?.trim() ? label : shortId(id);
}
