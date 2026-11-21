import { Alert, Anchor, type Failure } from "@maniumek/suwfrontkit";

export function PageFailed({ error }: Failure) {
    return (
        <Alert variant="destructive" className="flex flex-col gap-2">
            <p className="font-semibold">This page failed to open</p>
            {error instanceof Error && <p className="wrap-anywhere">{error.message}</p>}
            <div className="flex gap-4">
                <Anchor href={window.location.href}>Reload</Anchor>
                <Anchor href="/">Back to the list of agents</Anchor>
            </div>
        </Alert>
    );
}
