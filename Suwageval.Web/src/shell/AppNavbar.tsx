import { Box, NavLink } from "@mantine/core";

import { agentEvaluationHref, agentHref, agentVersionsHref } from "../routes/links";
import type { Navigation } from "./navigation";

/**
 * The sidebar: the list of agents, and below it the agent whose pages are open, with its Versions and Evaluation. The nesting is
 * drawn, not a NavLink with children: Mantine turns the click on a NavLink with children into a toggle, and these are all links.
 */
export function AppNavbar({ agentName, active }: Navigation) {
    return (
        <>
            <NavLink href="/" label="Agents" active={active === "agents"} />
            {agentName !== null && (
                <Box ml="md">
                    <NavLink href={agentHref(agentName)} label={agentName} />
                    <Box ml="md">
                        <NavLink href={agentVersionsHref(agentName)} label="Versions" active={active === "versions"} />
                        <NavLink href={agentEvaluationHref(agentName)} label="Evaluation" active={active === "evaluation"} />
                    </Box>
                </Box>
            )}
        </>
    );
}
