import { Box, Text } from "@mantine/core";
import type { ReactNode } from "react";

/**
 * A mark that says something only by its look (an icon) on the screen, and in words on paper, where an icon would be either
 * dropped or unexplained: the children show on the screen, `text` in brackets in print. The icon keeps its own accessible name.
 */
export function OnPaper({ text, children }: { text: string; children: ReactNode }) {
    return (
        <>
            <Box component="span" className="no-print">
                {children}
            </Box>
            <Text span inherit c="dimmed" className="print-only-inline">
                ({text})
            </Text>
        </>
    );
}
