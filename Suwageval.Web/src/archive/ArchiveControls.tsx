import { Alert, Button, Group } from "@mantine/core";
import type { ReactNode } from "react";

import { ToggleChip } from "../toggle/ToggleChip";

export interface ArchiveLabels {
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
                {archivedChip && (
                    <ToggleChip label="Archived" checked={showArchived} help={archivedChip.help} onChange={archivedChip.onChange} />
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
