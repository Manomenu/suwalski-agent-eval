import { Button, Group, Text } from "@mantine/core";
import { IconPrinter } from "@tabler/icons-react";

/**
 * The "Print" button of a view: opens the browser's print dialog, whose preview is the page itself (print.css changes it for paper;
 * "Save as PDF" is the dialog's). Not on paper. When `disabledReason` is given the button is off and the reason shows beside it.
 */
export function PrintButton({ disabledReason }: { disabledReason?: string | undefined }) {
    return (
        <Group gap="sm" className="no-print">
            <Button
                variant="default"
                leftSection={<IconPrinter size={16} aria-hidden />}
                disabled={disabledReason !== undefined}
                onClick={() => {
                    window.print();
                }}
            >
                Print
            </Button>
            {disabledReason !== undefined && (
                <Text size="sm" c="dimmed">
                    {disabledReason}
                </Text>
            )}
        </Group>
    );
}
