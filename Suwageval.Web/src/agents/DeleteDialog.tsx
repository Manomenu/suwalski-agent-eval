import { Alert, ConfirmDialog, Input, Label, Spinner } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";
import { useId, useState } from "react";

import { confirmsDeletion } from "./deletion";

export function DeleteDialog<Preview>({
    noun,
    title,
    subject,
    note,
    typedLabel,
    expected,
    previewKey,
    loadPreview,
    summary,
    remove,
    onClose,
    onDeleted,
}: {
    noun: string;
    title: string;
    subject: string;
    note?: string;
    typedLabel: string;
    expected: string;
    previewKey?: readonly unknown[];
    loadPreview: (signal: AbortSignal) => Promise<Preview>;
    summary: (preview: Preview) => string;
    remove: () => Promise<void>;
    onClose: () => void;
    onDeleted: () => void;
}) {
    const [typed, setTyped] = useState("");
    const [error, setError] = useState<string | null>(null);
    const preview = useQuery({
        queryKey: previewKey ?? ["deletion", noun, expected],
        queryFn: ({ signal }) => loadPreview(signal),
        gcTime: 0,
    });
    const inputId = useId();

    const confirm = async () => {
        setError(null);
        try {
            await remove();
        } catch (failure: unknown) {
            setError(failure instanceof Error ? failure.message : `The ${noun} could not be deleted.`);
            throw failure;
        }
        onDeleted();
    };

    return (
        <ConfirmDialog
            open
            onOpenChange={(open) => {
                if (!open) onClose();
            }}
            title={title}
            variant="destructive"
            confirmLabel="Delete"
            pendingLabel="Deleting…"
            confirmDisabled={!confirmsDeletion(typed, expected)}
            onConfirm={confirm}
        >
            <p>
                {noun[0]?.toUpperCase()}
                {noun.slice(1)} <b>{subject}</b> is deleted for good, with everything that belongs to it.
                {note === undefined ? "" : ` ${note}`}
            </p>
            {preview.isPending && <Spinner size="sm" label={`Counting what goes with the ${noun}`} />}
            {preview.isSuccess && <p>{summary(preview.data)}.</p>}
            {preview.isError && (
                <Alert variant="destructive">
                    <p className="font-semibold">{`The ${noun} cannot be deleted`}</p>
                    <p>{preview.error.message}</p>
                </Alert>
            )}
            <div className="flex flex-col gap-1">
                <Label htmlFor={inputId}>{typedLabel}</Label>
                <p className="text-sm text-muted-foreground">{expected}</p>
                <Input
                    id={inputId}
                    value={typed}
                    onChange={(event) => {
                        setTyped(event.currentTarget.value);
                    }}
                />
            </div>
            {error !== null && (
                <Alert variant="destructive">
                    <p className="font-semibold">{`The ${noun} could not be deleted`}</p>
                    <p>{error}</p>
                </Alert>
            )}
        </ConfirmDialog>
    );
}
