import { Anchor, Badge, Paper, formatDateTime } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";

import type { ReactNode } from "react";

import { versionQuery } from "../agents/api";
import { Page } from "../agents/Page";
import { jsonText } from "./view";
import { agentHref } from "../routes/links";
import { versionTitle } from "../format/names";

function CodeBlock({ children }: { children: string }) {
    return <pre className="overflow-x-auto rounded-md bg-secondary p-3 font-mono text-[13px]">{children}</pre>;
}

function Section({ title, children }: { title: string; children: ReactNode }) {
    return (
        <Paper role="region" aria-label={title} className="flex flex-col gap-3">
            <h3 className="text-[22px] font-bold">{title}</h3>
            {children}
        </Paper>
    );
}

export function VersionPage({ name, versionId }: { name: string; versionId: string }) {
    const query = useQuery(versionQuery(name, versionId));

    return (
        <Page query={query} notFound={`There is no version ${versionId} of agent ${name}.`}>
            {(version) => {
                const { content } = version;
                const events = content.context?.events ?? [];
                const systemTools = content.context?.tools ?? [];
                return (
                    <div className="flex flex-col gap-4">
                        <Anchor href={agentHref(name)}>← {name}</Anchor>
                        <h2 className="text-[26px] leading-tight font-bold">Version {versionTitle(version.id, content.label)}</h2>
                        <p className="text-muted-foreground">
                            id {version.id} · created {formatDateTime(version.createdAt)} · hash {version.contentHash}
                        </p>
                        {version.archivedAt != null && (
                            <Badge variant="neutral" className="w-fit">
                                Archived {formatDateTime(version.archivedAt)}
                            </Badge>
                        )}

                        {content.agents.map((agent) => (
                            <Section key={agent.name} title={`Agent ${agent.name}`}>
                                <h4 className="text-lg font-bold">Config</h4>
                                <CodeBlock>{agent.config}</CodeBlock>
                                <h4 className="text-lg font-bold">Model</h4>
                                <p>{agent.model.name}</p>
                                {agent.model.parameters != null && <CodeBlock>{jsonText(agent.model.parameters)}</CodeBlock>}
                                <h4 className="text-lg font-bold">Tools</h4>
                                {(agent.tools ?? []).length === 0 && <p className="text-muted-foreground">No tools.</p>}
                                {(agent.tools ?? []).map((tool) => (
                                    <div key={tool.name} className="flex flex-col gap-2.5">
                                        <p className="font-semibold">{tool.name}</p>
                                        <p>{tool.description}</p>
                                        <CodeBlock>{jsonText(tool.parameters)}</CodeBlock>
                                    </div>
                                ))}
                            </Section>
                        ))}

                        {systemTools.length > 0 && (
                            <Section title="Tools of the system's code">
                                <p className="text-muted-foreground">
                                    No agent owns these: the system calls them from its own code, no model chooses them.
                                </p>
                                {systemTools.map((tool) => (
                                    <div key={tool.name} className="flex flex-col gap-2.5">
                                        <p className="font-semibold">{tool.name}</p>
                                        <p>{tool.description}</p>
                                        <CodeBlock>{jsonText(tool.parameters)}</CodeBlock>
                                    </div>
                                ))}
                            </Section>
                        )}

                        {content.otherConfig != null && content.otherConfig !== "" && (
                            <Section title="Other config">
                                <CodeBlock>{content.otherConfig}</CodeBlock>
                            </Section>
                        )}

                        <Section title="Context">
                            <Badge variant="primary" className="w-fit">
                                {content.context?.usesRetrieval === true ? "uses retrieval" : "no retrieval"}
                            </Badge>
                            {events.length === 0 && <p className="text-muted-foreground">No events.</p>}
                            {events.map((event) => (
                                <div key={event.type} className="flex flex-col gap-2.5">
                                    <p className="font-semibold">Event {event.type}</p>
                                    <CodeBlock>{jsonText(event.dataSchema)}</CodeBlock>
                                </div>
                            ))}
                        </Section>
                    </div>
                );
            }}
        </Page>
    );
}
