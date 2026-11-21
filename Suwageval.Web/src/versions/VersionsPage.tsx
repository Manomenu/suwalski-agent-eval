import { Anchor, Button, Paper, formatDateTime } from "@maniumek/suwfrontkit";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { agentKeys, agentQuery, deleteAgent, fetchAgentDeletion, versionsQuery } from "../agents/api";
import { DeleteDialog } from "../agents/DeleteDialog";
import { agentDeletionSummary } from "../agents/deletion";
import { Page } from "../agents/Page";
import { VersionsSection } from "./VersionsSection";
import { useVersionFilter } from "./useVersionFilter";

export function VersionsPage({ name }: { name: string }) {
    const queryClient = useQueryClient();
    const [deleting, setDeleting] = useState(false);
    const filter = useVersionFilter();
    const agent = useQuery(agentQuery(name));
    const versions = useQuery(versionsQuery(name));
    const query = {
        isPending: agent.isPending || versions.isPending,
        error: agent.error ?? versions.error,
        data: agent.data !== undefined && versions.data !== undefined ? { agent: agent.data, versions: versions.data } : undefined,
    };

    return (
        <Page query={query} notFound={`There is no agent ${name}.`}>
            {({ agent, versions }) => (
                <div className="flex flex-col gap-5">
                    <Anchor href="/">← Agents</Anchor>
                    {deleting && (
                        <DeleteDialog
                            noun="agent"
                            title="Delete this agent?"
                            subject={agent.name}
                            note="Files in its repository are not touched, but the next push registers it again, empty."
                            typedLabel="Type the agent name to confirm"
                            expected={agent.name}
                            previewKey={agentKeys.deletion(agent.name)}
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
                    <Paper className="flex flex-col gap-1">
                        <div className="flex flex-wrap items-center justify-between gap-4">
                            <h2 className="text-[26px] leading-tight font-bold">{agent.name}</h2>
                            <Button
                                variant="destructive"
                                onClick={() => {
                                    setDeleting(true);
                                }}
                            >
                                Delete agent
                            </Button>
                        </div>
                        <p className="text-muted-foreground">
                            {agent.endpointUrl} · created {formatDateTime(agent.createdAt)}
                        </p>
                        <p className="text-muted-foreground">
                            versions {agent.versions} · scenarios {agent.scenarios} · checks {agent.checks}
                        </p>
                    </Paper>
                    <VersionsSection
                        agentName={agent.name}
                        versions={versions}
                        usage={filter.usage}
                        onUsageChange={filter.setUsage}
                        versionIds={filter.versionIds}
                        onVersionRemoved={filter.removeVersion}
                        environment={filter.environment}
                        onEnvironmentChange={filter.setEnvironment}
                        onChanged={() => {
                            void queryClient.invalidateQueries({ queryKey: agentKeys.agent(name) });
                        }}
                    />
                </div>
            )}
        </Page>
    );
}
