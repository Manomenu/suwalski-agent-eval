import { Anchor, Paper, Stack, Text, Title } from "@mantine/core";

import { fetchAgent } from "../../agents/api";
import { AreaOff } from "../../agents/AreaOff";
import { Page } from "../../agents/Page";
import { useRequest } from "../../api/useRequest";
import { ChecksSection } from "../checks/ChecksSection";
import { ConversationsSection } from "../conversations/ConversationsSection";
import { monitoringSettingsText } from "./settings";

/** `/agents/<name>/monitoring`: the conversations of the deployed agent and the checks run on them, when its monitoring is on. */
export function MonitoringPage({ name }: { name: string }) {
    const state = useRequest(name, (signal) => fetchAgent(name, signal));
    return (
        <Page state={state} notFound={`There is no agent ${name}.`}>
            {(agent) =>
                agent.features.monitoring == null ? (
                    <AreaOff area="monitoring" />
                ) : (
                    <Stack gap="lg">
                        <Anchor href="/">← Agents</Anchor>
                        <Paper withBorder p="md">
                            <Stack gap={4}>
                                <Title order={2}>{agent.name}</Title>
                                <Text c="dimmed">{monitoringSettingsText(agent.features.monitoring)}</Text>
                            </Stack>
                        </Paper>
                        <ChecksSection agent={agent.name} />
                        <ConversationsSection agent={agent.name} />
                    </Stack>
                )
            }
        </Page>
    );
}
