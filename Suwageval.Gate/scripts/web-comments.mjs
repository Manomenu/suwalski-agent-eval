#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

if (process.argv.includes("--help")) {
    console.log(`Fails on any comment in the web app's TypeScript that is not one line // Why: <reason> or a marker a tool reads
(eslint-disable, @ts-expect-error, @vite-ignore, prettier-ignore, /// <reference). Generated files stay out.`);
    process.exit(0);
}

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");
// Why: typescript is a dependency of the web app, and this script stands outside its package
const ts = createRequire(join(root, "Suwageval.Web/package.json"))("typescript");

// Why: inside JSX a comment can only be a block, so the same one line takes that form there
const why = /^(\/\/ Why: \S.*|\/\* Why: \S.* \*\/)$/;
const toolMarker = /^(\/\/\s*(eslint-disable|@ts-expect-error|prettier-ignore)|\/\*\s*(eslint-disable|@vite-ignore)|\/\/\/\s*<reference)/;
const task = /\b(TODO|FIXME|HACK|workaround|temporary)\b/i;

function problem(text) {
    if (toolMarker.test(text)) {
        return null;
    }
    if (task.test(text)) {
        return "a task marker";
    }
    return why.test(text) ? null : "not a Why";
}

function commentsIn(path, text) {
    const source = ts.createSourceFile(path, text, ts.ScriptTarget.Latest, false, path.endsWith("x") ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
    const found = new Map();
    const add = (ranges) => {
        for (const range of ranges ?? []) {
            found.set(range.pos, text.slice(range.pos, range.end));
        }
    };
    const visit = (node) => {
        if (node.getChildCount(source) === 0) {
            if (node.kind !== ts.SyntaxKind.JsxText) {
                add(ts.getLeadingCommentRanges(text, node.getFullStart()));
            }
            add(ts.getTrailingCommentRanges(text, node.getEnd()));
        }
        node.getChildren(source).forEach(visit);
    };
    visit(source);
    return [...found].map(([pos, comment]) => ({ line: source.getLineAndCharacterOfPosition(pos).line + 1, comment }));
}

const files = execFileSync("git", ["ls-files", "--cached", "--others", "--exclude-standard", "--", "Suwageval.Web/*.ts", "Suwageval.Web/*.tsx"], {
    cwd: root,
    encoding: "utf8",
})
    .split("\n")
    .filter((path) => path && !path.endsWith("src/api/openapi.d.ts"));

const problems = files.flatMap((path) => {
    let text;
    try {
        text = readFileSync(join(root, path), "utf8");
    } catch {
        return [];
    }
    return commentsIn(path, text)
        .map(({ line, comment }) => ({ line, comment, found: problem(comment) }))
        .filter(({ found }) => found)
        .map(({ line, comment, found }) => `${path}:${line}: ${found}: ${comment.split("\n")[0]}`);
});

if (problems.length > 0) {
    console.error("delete it, rename the code so it explains itself, move the task to TODO.md, or write `// Why: <reason>` on one line:");
    console.error(problems.join("\n"));
    process.exit(1);
}
