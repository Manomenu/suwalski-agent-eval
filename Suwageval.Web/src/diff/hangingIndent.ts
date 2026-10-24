// How a long YAML line wraps on screen: under the start of its value (the line diff and the definition drawer share it).

/**
 * How far a wrapped YAML line hangs, in characters: its indentation, a list item's `- ` and a `key: `, so the rest of a long
 * value wraps under the value's start, the way YAML itself indents.
 */
export function hangingIndent(line: string): number {
    const match = /^(\s*(?:- )*)(?:(?:"[^"]*"|[^\s:"#][^:#]*?):(?: |$))?/.exec(line);
    return match?.[0].length ?? 0;
}

/** A line that opens a block scalar: `key: |`, `- >-` and the like; the lines indented below it are its text. */
const BLOCK_SCALAR_HEADER = /(?::|^\s*-)\s+[|>][-+]?\d*[-+]?\s*$/;

/**
 * `hangingIndent` for every line of a YAML text, in order. The lines of a block scalar (a prompt, a simulated user) are text, not
 * keys: they hang by their indentation only, or a "word: " inside the prose would push the rest of the line off the screen.
 */
export function hangingIndents(lines: readonly string[]): number[] {
    // The indentation of the line that opened the block scalar being read, or null outside one.
    let scalarParent: number | null = null;
    return lines.map((line) => {
        const indentation = /^ */.exec(line)?.[0].length ?? 0;
        if (scalarParent !== null && (line.trim() === "" || indentation > scalarParent)) return indentation;
        scalarParent = BLOCK_SCALAR_HEADER.test(line) ? indentation : null;
        return hangingIndent(line);
    });
}
