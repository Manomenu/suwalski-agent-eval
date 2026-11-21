import { Anchor, ErrorScreen, type Failure } from "@maniumek/suwfrontkit";

export function PageFailed({ error }: Failure) {
    return (
        <ErrorScreen
            error={error}
            title="This page failed to open"
            actions={
                <>
                    <Anchor href={window.location.href}>Reload</Anchor>
                    <Anchor href="/">Back to the list of agents</Anchor>
                </>
            }
        />
    );
}
