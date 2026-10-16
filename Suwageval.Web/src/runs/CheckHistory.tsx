import { Anchor, Box, Group, Stack, Text, Tooltip } from "@mantine/core";

import type { CheckRunResult } from "./api";
import { CHECK_HISTORY_EXPLANATION, historyMarks, type HistoryMark } from "./regression";

const TOOLTIP_WIDTH = 340;

/** The size of a mark, in pixels: small enough to sit in a line of text. */
const MARK_SIZE = 12;

/** A passed check is a filled green dot, a failed one a red cross: told apart by shape, not only by colour. */
function Glyph({ passed }: { passed: boolean }) {
    return passed ? (
        <Box w={MARK_SIZE - 2} h={MARK_SIZE - 2} bg="green" style={{ borderRadius: "50%" }} />
    ) : (
        <Text span c="red" fw={900} fz={MARK_SIZE + 2} lh={1}>
            ✗
        </Text>
    );
}

function Mark({ mark }: { mark: HistoryMark }) {
    return (
        <Tooltip
            multiline
            w={TOOLTIP_WIDTH}
            label={
                <Stack gap={4}>
                    <Text size="sm" fw={600} inherit>
                        {mark.tooltip}
                    </Text>
                    <Text size="xs" inherit>
                        {CHECK_HISTORY_EXPLANATION}
                    </Text>
                </Stack>
            }
        >
            {mark.href === null ? (
                <Box component="span" aria-label={mark.label} role="img" display="inline-flex">
                    <Glyph passed={mark.passed} />
                </Box>
            ) : (
                <Anchor href={mark.href} aria-label={mark.label} display="inline-flex">
                    <Glyph passed={mark.passed} />
                </Anchor>
            )}
        </Tooltip>
    );
}

/** A check's verdicts over its history, oldest first, and this run's verdict last. Nothing for a check without history. */
export function CheckHistory({ check }: { check: Pick<CheckRunResult, "passed" | "regression"> }) {
    const marks = historyMarks(check);
    if (marks.length === 0) return null;
    return (
        <Group gap={4} wrap="nowrap" role="group" aria-label="History of the check">
            {marks.map((mark, index) => (
                <Group key={mark.key} gap={4} wrap="nowrap">
                    {index === marks.length - 1 && (
                        <Text span c="dimmed" size="xs">
                            →
                        </Text>
                    )}
                    <Mark mark={mark} />
                </Group>
            ))}
        </Group>
    );
}
