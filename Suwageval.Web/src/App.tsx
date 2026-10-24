import { Anchor, AppShell, Box, Burger, Group, Image, Title } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { lazy, Suspense, useState } from "react";

import { AgentsPage } from "./agents/AgentsPage";
import { EvaluationPage } from "./evaluation/agent/EvaluationPage";
import { ComparePage } from "./evaluation/compare/ComparePage";
import { MonitoringPage } from "./monitoring/agent/MonitoringPage";
import { ServerStatus } from "./health/ServerStatus";
import { pageOf } from "./routes/pageOf";
import { RunPage } from "./evaluation/runs/RunPage";
import { AppNavbar } from "./shell/AppNavbar";
import { contentMaxWidth, HEADER_HEIGHT, LOGO_SIZE, NAVBAR_WIDTH } from "./shell/layout";
import { navigationOf } from "./shell/navigation";
import { ReportAgentContext } from "./shell/reportAgent";
import { VersionPage } from "./versions/VersionPage";
import { VersionsPage } from "./versions/VersionsPage";

// The diff library is needed only here: the page loads when a comparison of versions is opened.
const VersionComparePage = lazy(() => import("./versions/VersionComparePage").then((module) => ({ default: module.VersionComparePage })));

/** Puts the page together from features: the shell (header, sidebar), then the page the path opens. */
export function App() {
    const page = pageOf(window.location.pathname, window.location.search);
    const [navbarOpened, { toggle }] = useDisclosure(false);
    // The agent of a page whose address does not name it (a run, a comparison): the page reports it once it has loaded.
    const [agentOfPage, setAgentOfPage] = useState<string | null>(null);

    return (
        <AppShell
            header={{ height: HEADER_HEIGHT }}
            navbar={{ width: NAVBAR_WIDTH, breakpoint: "sm", collapsed: { mobile: !navbarOpened } }}
            padding="md"
        >
            <AppShell.Header className="no-print">
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
            <AppShell.Navbar p="xs" className="no-print">
                <AppNavbar {...navigationOf(page, agentOfPage)} />
            </AppShell.Navbar>
            <AppShell.Main>
                <ReportAgentContext.Provider value={setAgentOfPage}>
                    <Box maw={contentMaxWidth(page.kind)} className="print-content">
                        {page.kind === "agents" && <AgentsPage />}
                        {page.kind === "agentEvaluation" && <EvaluationPage name={page.name} />}
                        {page.kind === "agentMonitoring" && <MonitoringPage name={page.name} />}
                        {page.kind === "agentVersions" && <VersionsPage name={page.name} />}
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
                </ReportAgentContext.Provider>
            </AppShell.Main>
        </AppShell>
    );
}
