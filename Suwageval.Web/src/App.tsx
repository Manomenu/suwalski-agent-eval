import { Anchor, Container, Group } from "@mantine/core";

import { AgentPage } from "./agents/AgentPage";
import { AgentsPage } from "./agents/AgentsPage";
import { VersionPage } from "./agents/VersionPage";
import { ServerStatus } from "./health/ServerStatus";
import { pageOf } from "./route";
import { RunPage } from "./runs/RunPage";

/** Puts the page together from features: the header, then the page the path opens. */
export function App() {
    const page = pageOf(window.location.pathname);

    return (
        <>
            <Container size="lg" py="sm">
                <Group justify="space-between">
                    <Anchor href="/" fw={700} size="lg" underline="never">
                        suwageval
                    </Anchor>
                    <ServerStatus />
                </Group>
            </Container>
            {page.kind === "agents" && <AgentsPage />}
            {page.kind === "agent" && <AgentPage name={page.name} />}
            {page.kind === "version" && <VersionPage name={page.name} versionId={page.versionId} />}
            {page.kind === "run" && <RunPage runId={page.runId} />}
            {page.kind === "unknown" && (
                <Container py="xl">
                    <Anchor href="/">Page not found — back to the list of agents</Anchor>
                </Container>
            )}
        </>
    );
}
