import { Center, Stack, Title } from "@mantine/core";

import { ServerStatus } from "./health/ServerStatus";
import { RunPage } from "./runs/RunPage";
import { runIdOf } from "./runs/route";

/** Puts the page together from features. Replace the content with the first real screen. */
export function App() {
    const runId = runIdOf(window.location.pathname);
    if (runId !== null) return <RunPage runId={runId} />;

    return (
        <Center h="100vh">
            <Stack align="center">
                <Title order={2}>suwageval</Title>
                <ServerStatus />
            </Stack>
        </Center>
    );
}
