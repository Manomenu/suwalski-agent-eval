import { Box, NavLink } from "@mantine/core";

import { areaOn } from "../agents/features";
import { useAgentFeatures } from "../agents/useAgentFeatures";
import { agentEvaluationHref, agentHref, agentMonitoringHref, agentVersionsHref } from "../routes/links";
import type { Navigation } from "./navigation";

/**
 * The sidebar: the list of agents, and below it the agent whose pages are open, with its Versions and the areas it uses (Evaluation
 * unless switched off, Monitoring once switched on: `features:` in its agent.yaml). The nesting is drawn, not a NavLink with children:
 * Mantine turns the click on a NavLink with children into a toggle, and these are all links.
 */
export function AppNavbar({ agentName, active }: Navigation) {
    const features = useAgentFeatures(agentName);
    // Until the agent is read, the defaults: Evaluation on, Monitoring off.
    const evaluation = features === null || areaOn(features, "evaluation");
    const monitoring = features !== null && areaOn(features, "monitoring");
    return (
        <>
            <NavLink href="/" label="Agents" active={active === "agents"} />
            {agentName !== null && (
                <Box ml="md">
                    <NavLink href={agentHref(agentName)} label={agentName} />
                    <Box ml="md">
                        <NavLink href={agentVersionsHref(agentName)} label="Versions" active={active === "versions"} />
                        {evaluation && (
                            <NavLink href={agentEvaluationHref(agentName)} label="Evaluation" active={active === "evaluation"} />
                        )}
                        {monitoring && (
                            <NavLink href={agentMonitoringHref(agentName)} label="Monitoring" active={active === "monitoring"} />
                        )}
                    </Box>
                </Box>
            )}
        </>
    );
}
