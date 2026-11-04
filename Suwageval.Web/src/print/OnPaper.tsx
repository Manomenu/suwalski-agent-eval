import { Box, Text } from "@mantine/core";
import type { ReactNode } from "react";

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
