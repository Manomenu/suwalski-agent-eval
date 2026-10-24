import { Anchor, Button, Group, Paper, Stack, Text, Title } from "@mantine/core";
import { useState } from "react";

import { deleteAgent, fetchAgent, fetchAgentDeletion, fetchVersions } from "../agents/api";
import { DeleteModal } from "../agents/DeleteModal";
import { agentDeletionSummary } from "../agents/deletion";
import { formatTime } from "../format/time";
import { Page } from "../agents/Page";
import { useRequest } from "../api/useRequest";
import { VersionsSection } from "./VersionsSection";
import { useVersionGrouping } from "./useVersionGrouping";

/** `/agents/<name>/versions`: the agent's versions, to archive, restore, delete and compare. */
export function VersionsPage({ name }: { name: string }) {
    // Bumped after versions are archived or restored: the list is loaded again (the page stays until the new one arrives).
    const [reload, setReload] = useState(0);
    const [showArchived, setShowArchived] = useState(false);
    const [deleting, setDeleting] = useState(false);
    const [grouping, setGrouping] = useVersionGrouping();
    const state = useRequest(`${name}/${String(reload)}`, async (signal) => {
        const [agent, versions] = await Promise.all([fetchAgent(name, signal), fetchVersions(name, signal)]);
        return { agent, versions };
    });

    return (
        <Page state={state} notFound={`There is no agent ${name}.`}>
            {({ agent, versions }) => (
                <Stack gap="lg">
                    <Anchor href="/">← Agents</Anchor>
                    {deleting && (
                        <DeleteModal
                            noun="agent"
                            title="Delete this agent?"
                            subject={agent.name}
                            note="Files in its repository are not touched, but the next push registers it again, empty."
                            typedLabel="Type the agent name to confirm"
                            expected={agent.name}
                            loadPreview={(signal) => fetchAgentDeletion(agent.name, signal)}
                            summary={agentDeletionSummary}
                            remove={() => deleteAgent(agent.name)}
                            onClose={() => {
                                setDeleting(false);
                            }}
                            onDeleted={() => {
                                window.location.assign("/");
                            }}
                        />
                    )}
                    <Paper withBorder p="md">
                        <Stack gap={4}>
                            <Group justify="space-between">
                                <Title order={2}>{agent.name}</Title>
                                <Button
                                    color="red"
                                    variant="light"
                                    onClick={() => {
                                        setDeleting(true);
                                    }}
                                >
                                    Delete agent
                                </Button>
                            </Group>
                            <Text c="dimmed">
                                {agent.endpointUrl} · created {formatTime(agent.createdAt)}
                            </Text>
                            <Text c="dimmed">
                                versions {agent.versions} · scenarios {agent.scenarios} · checks {agent.checks}
                            </Text>
                        </Stack>
                    </Paper>
                    <VersionsSection
                        agentName={agent.name}
                        versions={versions}
                        showArchived={showArchived}
                        onShowArchivedChange={setShowArchived}
                        grouping={grouping}
                        onGroupingChange={setGrouping}
                        onChanged={() => {
                            setReload(reload + 1);
                        }}
                    />
                </Stack>
            )}
        </Page>
    );
}
