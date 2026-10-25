import { Anchor, Badge, Group, Paper, Stack, Text, Title } from "@mantine/core";

import { Page } from "../../agents/Page";
import { useRequest } from "../../api/useRequest";
import { ConversationItems } from "../../conversation/ConversationItems";
import { DataVersion } from "../../dataVersion/DataVersion";
import { formatTime } from "../../format/time";
import { agentMonitoringHref, versionInMonitoringHref } from "../../routes/links";
import { useReportAgent } from "../../shell/reportAgent";
import { fetchConversation, type ConversationDetail } from "./api";
import { versionText } from "./conversations";
import { ScenarioDraftButton } from "./ScenarioDraftButton";
import { ModelJudgedIcon } from "../../verdict/ModelJudgedIcon";
import { failedEvidence, verdictOf } from "../../verdict/verdict";
import { VerdictBadge } from "../../verdict/VerdictBadge";
import { VerdictMessage } from "../../verdict/VerdictMessage";

/** `/conversations/<id>`: one monitored conversation, its turns one under another with what the agent did in each. */
export function ConversationPage({ conversationId }: { conversationId: string }) {
    const state = useRequest(conversationId, (signal) => fetchConversation(conversationId, signal));
    useReportAgent(state.kind === "loaded" ? state.value.conversation.agent : null);
    return (
        <Page state={state} notFound={`There is no conversation ${conversationId}.`}>
            {(detail) => <View detail={detail} />}
        </Page>
    );
}

function View({ detail }: { detail: ConversationDetail }) {
    const { conversation, turns } = detail;
    // What failed checks point at is marked in the turns, as on a run's page.
    const evidence = failedEvidence(detail.checks ?? []);
    return (
        <Stack gap="md">
            <Group gap="xs" className="no-print">
                <Anchor href="/">Agents</Anchor>/<Anchor href={agentMonitoringHref(conversation.agent)}>{conversation.agent}</Anchor>
            </Group>
            <Group gap="sm">
                <Title order={2} className="wrap-key">
                    Conversation {conversation.conversationId}
                </Title>
                <Badge size="lg" variant="light" color={conversation.state === "Open" ? "blue" : "gray"}>
                    {conversation.state}
                </Badge>
                <ScenarioDraftButton
                    conversationId={conversation.id}
                    items={turns.flatMap((turn) => turn.items)}
                    failed={detail.checks?.some((check) => !check.passed && check.notJudged !== true) ?? false}
                />
            </Group>
            <Text c="dimmed">
                {conversation.environment} · {conversation.turns} turns · version{" "}
                <Anchor href={versionInMonitoringHref(conversation.agent, conversation.agentVersionId)}>{versionText(conversation)}</Anchor>{" "}
                · from {formatTime(conversation.startedAt)} to {formatTime(conversation.lastTurnAt)}
            </Text>
            {detail.checks != null && detail.checks.length > 0 && (
                <Paper withBorder p="md">
                    <Stack gap="xs">
                        <Title order={4}>Monitoring checks</Title>
                        {detail.checks.map((check) => (
                            <Stack key={check.key} gap={4}>
                                <Group justify="space-between" wrap="nowrap" align="flex-start">
                                    <Group gap="xs">
                                        <VerdictBadge verdict={verdictOf(check)} />
                                        <Text fw={600} className="wrap-key">
                                            {check.key}
                                        </Text>
                                    </Group>
                                    {check.judgeModel != null && <ModelJudgedIcon model={check.judgeModel} />}
                                </Group>
                                <Text size="sm">
                                    <VerdictMessage
                                        message={check.message}
                                        byModel={check.judgeModel != null && check.notJudged !== true}
                                    />
                                </Text>
                            </Stack>
                        ))}
                    </Stack>
                </Paper>
            )}
            {turns.map((turn, index) => (
                <Paper key={turn.id} withBorder p="md">
                    <Stack gap="xs">
                        <Group gap="sm">
                            <Title order={4}>Turn {index + 1}</Title>
                            <Text c="dimmed" size="sm">
                                {formatTime(turn.at)}
                            </Text>
                            {turn.agentVersionId !== conversation.agentVersionId && (
                                <Anchor size="sm" href={versionInMonitoringHref(conversation.agent, turn.agentVersionId)}>
                                    version {turn.agentVersionId.slice(0, 8)}
                                </Anchor>
                            )}
                        </Group>
                        {turn.dataVersion != null && <DataVersion dataVersion={turn.dataVersion} />}
                        <ConversationItems items={turn.items} highlight={evidence} />
                    </Stack>
                </Paper>
            ))}
        </Stack>
    );
}
