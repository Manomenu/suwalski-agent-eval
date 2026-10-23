import { Anchor, AppShell, Box, Burger, Group, Image, Title } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { lazy, Suspense } from "react";

import { AgentPage } from "./agents/AgentPage";
import { AgentsPage } from "./agents/AgentsPage";
import { VersionPage } from "./agents/versions/VersionPage";
import { ComparePage } from "./compare/ComparePage";
import { ServerStatus } from "./health/ServerStatus";
import { pageOf } from "./route";
import { RunPage } from "./runs/RunPage";
import { AppNavbar } from "./shell/AppNavbar";
import { CONTENT_MAX_WIDTH, HEADER_HEIGHT, LOGO_SIZE, NAVBAR_WIDTH } from "./shell/layout";

// The diff library is needed only here: the page loads when a comparison of versions is opened.
const VersionComparePage = lazy(() =>
    import("./agents/versions/VersionComparePage").then((module) => ({ default: module.VersionComparePage })),
);

/** Puts the page together from features: the shell (header, sidebar), then the page the path opens. */
export function App() {
    const page = pageOf(window.location.pathname, window.location.search);
    const [navbarOpened, { toggle }] = useDisclosure(false);

    return (
        <AppShell
            header={{ height: HEADER_HEIGHT }}
            navbar={{ width: NAVBAR_WIDTH, breakpoint: "sm", collapsed: { mobile: !navbarOpened } }}
            padding="md"
        >
            <AppShell.Header>
                <Group h="100%" px="md" justify="space-between" wrap="nowrap">
                    <Group gap="sm" wrap="nowrap">
                        <Burger opened={navbarOpened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Menu" />
                        <Anchor href="/" underline="never" c="inherit">
                            <Group gap="xs" wrap="nowrap">
                                {/* The app icon, decorative: the name beside it is the link's text. */}
                                <Image src="/favicon.svg" alt="" w={LOGO_SIZE} h={LOGO_SIZE} />
                                <Title order={4} component="span">
                                    suwageval
                                </Title>
                            </Group>
                        </Anchor>
                    </Group>
                    <ServerStatus />
                </Group>
            </AppShell.Header>
            <AppShell.Navbar p="xs">
                <AppNavbar
                    agentsActive={page.kind === "agents"}
                    agentName={page.kind === "agent" || page.kind === "version" || page.kind === "versionCompare" ? page.name : null}
                    agentActive={page.kind === "agent"}
                />
            </AppShell.Navbar>
            <AppShell.Main>
                {/* The agent's page sets its own widths: its analytics reach the right edge on a wide screen (app.css). */}
                <Box maw={page.kind === "agent" ? undefined : CONTENT_MAX_WIDTH}>
                    {page.kind === "agents" && <AgentsPage />}
                    {page.kind === "agent" && <AgentPage name={page.name} />}
                    {page.kind === "version" && <VersionPage name={page.name} versionId={page.versionId} />}
                    {page.kind === "versionCompare" && (
                        <Suspense fallback={null}>
                            <VersionComparePage name={page.name} a={page.a} b={page.b} />
                        </Suspense>
                    )}
                    {page.kind === "run" && <RunPage runId={page.runId} />}
                    {page.kind === "compare" && (
                        <ComparePage runs={page.runs} group={page.group} agent={page.agent} baseline={page.baseline} />
                    )}
                    {page.kind === "unknown" && <Anchor href="/">Page not found — back to the list of agents</Anchor>}
                </Box>
            </AppShell.Main>
        </AppShell>
    );
}
