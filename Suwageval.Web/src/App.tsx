import { Anchor, AppShell } from "@maniumek/suwfrontkit";
import { lazy, Suspense, useState } from "react";

import { AgentsPage } from "./agents/AgentsPage";
import { EvaluationPage } from "./evaluation/agent/EvaluationPage";
import { ComparePage } from "./evaluation/compare/ComparePage";
import { MonitoringPage } from "./monitoring/agent/MonitoringPage";
import { ConversationPage } from "./monitoring/conversations/ConversationPage";
import { ServerStatus } from "./health/ServerStatus";
import { pageOf } from "./routes/pageOf";
import { RunPage } from "./evaluation/runs/RunPage";
import { AppNavbar } from "./shell/AppNavbar";
import { ErrorBoundary } from "@maniumek/suwfrontkit";
import { PageFailed } from "./shell/PageFailed";
import { contentMaxWidth } from "./shell/layout";
import { navigationOf } from "./shell/navigation";
import { ReportAgentContext } from "./shell/reportAgent";
import { VersionPage } from "./versions/VersionPage";
import { VersionsPage } from "./versions/VersionsPage";

const VersionComparePage = lazy(() => import("./versions/VersionComparePage").then((module) => ({ default: module.VersionComparePage })));

export function App() {
    const page = pageOf(window.location.pathname, window.location.search);
    const [agentOfPage, setAgentOfPage] = useState<string | null>(null);

    return (
        <AppShell
            appIcon={<img src="/favicon.svg" alt="" />}
            appTitle="suwageval"
            home={(link) => <a href="/" {...link} />}
            headerEnd={<ServerStatus />}
            panel={<AppNavbar {...navigationOf(page, agentOfPage)} />}
            panelLabel="Navigation panel"
            storageKey="suwageval.navigation-panel"
        >
            <ReportAgentContext value={setAgentOfPage}>
                <div style={{ maxWidth: contentMaxWidth(page.kind) }} className="print-content">
                    <ErrorBoundary key={window.location.pathname} fallback={PageFailed}>
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
                        {page.kind === "conversation" && <ConversationPage conversationId={page.conversationId} />}
                        {page.kind === "compare" && (
                            <ComparePage runs={page.runs} group={page.group} agent={page.agent} baseline={page.baseline} />
                        )}
                        {page.kind === "unknown" && <Anchor href="/">Page not found — back to the list of agents</Anchor>}
                    </ErrorBoundary>
                </div>
            </ReportAgentContext>
        </AppShell>
    );
}
