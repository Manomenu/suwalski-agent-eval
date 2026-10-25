import { Button, Group } from "@mantine/core";
import { IconPrinter } from "@tabler/icons-react";

/**
 * The "Print" button of a view: opens the browser's print dialog, whose preview is the page itself (print.css changes it for paper;
 * "Save as PDF" is the dialog's). Not on paper.
 */
export function PrintButton() {
    return (
        <Group gap="sm" className="no-print">
            <Button
                variant="default"
                leftSection={<IconPrinter size={16} aria-hidden />}
                onClick={() => {
                    window.print();
                }}
            >
                Print
            </Button>
        </Group>
    );
}
