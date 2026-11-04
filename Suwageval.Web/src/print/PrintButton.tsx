import { Button, Group } from "@mantine/core";
import { IconPrinter } from "@tabler/icons-react";

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
