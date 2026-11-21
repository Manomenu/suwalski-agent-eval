import type { ReactNode } from "react";

export function OnPaper({ text, children }: { text: string; children: ReactNode }) {
    return (
        <>
            <span className="no-print">{children}</span>
            <span className="print-only-inline text-muted-foreground">({text})</span>
        </>
    );
}
