import { Anchor, Paper } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";

import { agentQuery } from "../../agents/api";
import { AreaOff } from "../../agents/AreaOff";
import { Page } from "../../agents/Page";
import { versionsInMonitoringHref } from "../../routes/links";
import { ChecksSection } from "../checks/ChecksSection";
import { ConversationsSection } from "../conversations/ConversationsSection";
import { monitoringSettingsText } from "./settings";
import { useEnvironmentFilter } from "./useEnvironmentFilter";

export function MonitoringPage({ name }: { name: string }) {
    const query = useQuery(agentQuery(name));
    const [environment, setEnvironment] = useEnvironmentFilter();
    return (
        <Page query={query} notFound={`There is no agent ${name}.`}>
            {(agent) =>
                agent.features.monitoring == null ? (
                    <AreaOff area="monitoring" />
                ) : (
                    <div className="flex flex-col gap-5">
                        <Anchor href="/">← Agents</Anchor>
                        <Paper className="flex flex-col gap-1">
                            <div className="flex flex-wrap items-center justify-between gap-4">
                                <h2 className="text-[26px] leading-tight font-bold">{agent.name}</h2>
                                <Anchor href={versionsInMonitoringHref(agent.name)}>Versions in monitoring</Anchor>
                            </div>
                            <p className="text-muted-foreground">{monitoringSettingsText(agent.features.monitoring)}</p>
                        </Paper>
                        <ChecksSection agent={agent.name} environment={environment} onEnvironmentChange={setEnvironment} />
                        <ConversationsSection key={environment ?? ""} agent={agent.name} environment={environment} />
                    </div>
                )
            }
        </Page>
    );
}
