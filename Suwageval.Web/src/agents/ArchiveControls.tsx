import { Alert, Button, Group, Switch } from "@mantine/core";
import type { ReactNode } from "react";

/** The texts that differ between the lists that are archived (versions, run groups). */
export interface ArchiveLabels {
    archive: string;
    unarchive: string;
    remove: string;
    /** Why "Delete" is disabled. */
    removeHint: string;
    showArchived: string;
    errorTitle: string;
}

/**
 * The toolbar above an archivable list: archive and restore the ticked rows, delete one archived row, "Show archived" (unless the list has its own control for it), and the notice
 * or error of the last change. `extra` is a button of the list itself, placed after "Archive".
 */
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
    onShowArchivedChange,
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
    /** Absent when the list has its own way to show archived rows (the Versions page's Archived usage chip): no switch then. */
    onShowArchivedChange?: (show: boolean) => void;
}) {
    return (
        <>
            <Group justify="space-between">
                <Group gap="sm">
                    <Button
                        variant="light"
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
                            variant="light"
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
                            variant="light"
                            disabled={!canDelete || busy}
                            title={canDelete ? undefined : labels.removeHint}
                            onClick={onDelete}
                        >
                            {labels.remove}
                        </Button>
                    )}
                </Group>
                {onShowArchivedChange && (
                    <Switch
                        label={labels.showArchived}
                        checked={showArchived}
                        onChange={(event) => {
                            onShowArchivedChange(event.currentTarget.checked);
                        }}
                    />
                )}
            </Group>
            {notice !== null && <Alert color="green">{notice}</Alert>}
            {error !== null && (
                <Alert color="red" title={labels.errorTitle}>
                    {error}
                </Alert>
            )}
        </>
    );
}
