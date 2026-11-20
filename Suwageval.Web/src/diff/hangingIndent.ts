export function hangingIndent(line: string): number {
    const match = /^(\s*(?:- )*)(?:(?:"[^"]*"|[^\s:"#][^:#]*?):(?: |$))?/.exec(line);
    return match?.[0].length ?? 0;
}

const BLOCK_SCALAR_HEADER = /(?::|^\s*-)\s+[|>][-+]?\d*[-+]?\s*$/;

export function hangingIndents(lines: readonly string[]): number[] {
    let scalarParent: number | null = null;
    return lines.map((line) => {
        const indentation = /^ */.exec(line)?.[0].length ?? 0;
        if (scalarParent !== null && (line.trim() === "" || indentation > scalarParent)) return indentation;
        scalarParent = BLOCK_SCALAR_HEADER.test(line) ? indentation : null;
        return hangingIndent(line);
    });
}
