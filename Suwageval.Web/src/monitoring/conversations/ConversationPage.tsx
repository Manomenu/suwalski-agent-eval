import { Anchor, Badge, formatDateTime, Paper } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";

import { Page } from "../../agents/Page";
import { ConversationItems } from "../../conversation/ConversationItems";
import { DataVersion } from "../../dataVersion/DataVersion";
import { agentMonitoringHref, versionInMonitoringHref } from "../../routes/links";
import { useReportAgent } from "../../shell/reportAgent";
import { conversationQuery, type ConversationDetail } from "./api";
import { versionText } from "./conversations";
import { ScenarioDraftButton } from "./ScenarioDraftButton";
import { ModelJudgedIcon } from "../../verdict/ModelJudgedIcon";
import { failedEvidence, verdictOf } from "../../verdict/verdict";
import { VerdictBadge } from "../../verdict/VerdictBadge";
import { VerdictMessage } from "../../verdict/VerdictMessage";

export function ConversationPage({ conversationId }: { conversationId: string }) {
    const query = useQuery(conversationQuery(conversationId));
    useReportAgent(query.data?.conversation.agent ?? null);
    return (
        <Page query={query} notFound={`There is no conversation ${conversationId}.`}>
            {(detail) => <View detail={detail} />}
        </Page>
    );
}

function View({ detail }: { detail: ConversationDetail }) {
    const { conversation, turns } = detail;
    const evidence = failedEvidence(detail.checks ?? []);
    return (
        <div className="flex flex-col gap-4">
            <div className="no-print flex flex-wrap items-center gap-2.5">
                <Anchor href="/">Agents</Anchor>/<Anchor href={agentMonitoringHref(conversation.agent)}>{conversation.agent}</Anchor>
            </div>
            <div className="flex flex-wrap items-center gap-3">
                <h2 className="wrap-key text-[26px] leading-tight font-bold">Conversation {conversation.conversationId}</h2>
                <Badge variant={conversation.state === "Open" ? "info" : "neutral"}>{conversation.state}</Badge>
                <ScenarioDraftButton
                    conversationId={conversation.id}
                    items={turns.flatMap((turn) => turn.items)}
                    failed={detail.checks?.some((check) => !check.passed && check.notJudged !== true) ?? false}
                />
            </div>
            <p className="text-muted-foreground">
                {conversation.environment} · {conversation.turns} turns · version{" "}
                <Anchor href={versionInMonitoringHref(conversation.agent, conversation.agentVersionId)}>{versionText(conversation)}</Anchor>{" "}
                · from {formatDateTime(conversation.startedAt)} to {formatDateTime(conversation.lastTurnAt)}
            </p>
            {detail.checks != null && detail.checks.length > 0 && (
                <Paper className="flex flex-col gap-2.5">
                    <h4 className="text-lg font-bold">Monitoring checks</h4>
                    {detail.checks.map((check) => (
                        <div key={check.key} className="flex flex-col gap-1">
                            <div className="flex items-start justify-between">
                                <div className="flex flex-wrap items-center gap-2.5">
                                    <VerdictBadge verdict={verdictOf(check)} />
                                    <span className="wrap-key font-semibold">{check.key}</span>
                                </div>
                                {check.judgeModel != null && <ModelJudgedIcon model={check.judgeModel} />}
                            </div>
                            <p className="text-sm">
                                <VerdictMessage message={check.message} byModel={check.judgeModel != null && check.notJudged !== true} />
                            </p>
                        </div>
                    ))}
                </Paper>
            )}
            {turns.map((turn, index) => (
                <Paper key={turn.id} className="flex flex-col gap-2.5">
                    <div className="flex flex-wrap items-center gap-3">
                        <h4 className="text-lg font-bold">Turn {index + 1}</h4>
                        <span className="text-sm text-muted-foreground">{formatDateTime(turn.at)}</span>
                        {turn.agentVersionId !== conversation.agentVersionId && (
                            <Anchor className="text-sm" href={versionInMonitoringHref(conversation.agent, turn.agentVersionId)}>
                                version {turn.agentVersionId.slice(0, 8)}
                            </Anchor>
                        )}
                    </div>
                    {turn.dataVersion != null && <DataVersion dataVersion={turn.dataVersion} />}
                    <ConversationItems items={turn.items} highlight={evidence} />
                </Paper>
            ))}
        </div>
    );
}
