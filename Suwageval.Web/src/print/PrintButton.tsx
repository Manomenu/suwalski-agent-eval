import { Button } from "@maniumek/suwfrontkit";
import { IconPrinter } from "@tabler/icons-react";

export function PrintButton() {
    return (
        <div className="no-print flex flex-wrap items-center gap-3">
            <Button
                variant="outline"
                onClick={() => {
                    window.print();
                }}
            >
                <IconPrinter size={16} aria-hidden />
                Print
            </Button>
        </div>
    );
}
