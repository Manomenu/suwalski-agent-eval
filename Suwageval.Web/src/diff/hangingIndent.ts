export function hangingIndent(line: string): number {
    const match = /^(\s*(?:- )*)(?:(?:"[^"]*"|[^\s:"#][^:#]*?):(?: |$))?/.exec(line);
    return match?.[0].length ?? 0;
}

const BLOCK_SCALAR_HEADER = /(?::|^\s*-)\s+[|>][-+]?\d*[-+]?\s*$/;

// Why: lines of a block scalar hang by indentation only, or a word followed by a colon inside the prose would push the rest of the line off the screen
export function hangingIndents(lines: readonly string[]): number[] {
    let scalarParent: number | null = null;
    return lines.map((line) => {
        const indentation = /^ */.exec(line)?.[0].length ?? 0;
        if (scalarParent !== null && (line.trim() === "" || indentation > scalarParent)) return indentation;
        scalarParent = BLOCK_SCALAR_HEADER.test(line) ? indentation : null;
        return hangingIndent(line);
    });
}
