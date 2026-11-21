import { Alert, Button, ToggleChip } from "@maniumek/suwfrontkit";
import type { ReactNode } from "react";

export interface ArchiveLabels {
    group: string;
    archive: string;
    unarchive: string;
    remove: string;
    removeHint: string;
    errorTitle: string;
}

export function ArchiveControls({
    labels,
    toArchive,
    toRestore,
    canDelete,
    busy,
    showArchived,
    notice,
    error,
    extra,
    onChange,
    onDelete,
    archivedChip,
}: {
    labels: ArchiveLabels;
    toArchive: string[];
    toRestore: string[];
    canDelete: boolean;
    busy: boolean;
    showArchived: boolean;
    notice: string | null;
    error: string | null;
    extra?: ReactNode;
    onChange: (keys: string[], archived: boolean) => void;
    onDelete: () => void;
    archivedChip?: { help: string; onChange: (show: boolean) => void };
}) {
    return (
        <>
            <div role="group" aria-label={labels.group} className="flex flex-wrap items-center justify-between gap-4">
                <div className="flex flex-wrap items-center gap-2">
                    <Button
                        variant="outline"
                        disabled={toArchive.length === 0 || busy}
                        onClick={() => {
                            onChange(toArchive, true);
                        }}
                    >
                        {labels.archive}
                    </Button>
                    {extra}
                    {showArchived && (
                        <Button
                            variant="outline"
                            disabled={toRestore.length === 0 || busy}
                            onClick={() => {
                                onChange(toRestore, false);
                            }}
                        >
                            {labels.unarchive}
                        </Button>
                    )}
                    {showArchived && (
                        <Button
                            color="red"
                            variant="outline"
                            disabled={!canDelete || busy}
                            title={canDelete ? undefined : labels.removeHint}
                            onClick={onDelete}
                        >
                            {labels.remove}
                        </Button>
                    )}
                </div>
                {archivedChip && (
                    <ToggleChip pressed={showArchived} help={archivedChip.help} onPressedChange={archivedChip.onChange}>
                        Archived
                    </ToggleChip>
                )}
            </div>
            {notice !== null && <Alert>{notice}</Alert>}
            {error !== null && (
                <Alert variant="destructive">
                    <p className="font-semibold">{labels.errorTitle}</p>
                    <p>{error}</p>
                </Alert>
            )}
        </>
    );
}
