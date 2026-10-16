import { NavLink } from "@mantine/core";

/** The sidebar: the list of agents, and below it the agent whose pages are open (`agentName`, and whether its own page is). */
export function AppNavbar({
    agentsActive,
    agentName,
    agentActive,
}: {
    agentsActive: boolean;
    agentName: string | null;
    agentActive: boolean;
}) {
    return (
        <NavLink href="/" label="Agents" active={agentsActive} defaultOpened={agentName !== null} childrenOffset="md">
            {agentName !== null && <NavLink href={`/agents/${encodeURIComponent(agentName)}`} label={agentName} active={agentActive} />}
        </NavLink>
    );
}
