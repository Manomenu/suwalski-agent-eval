import { Box, NavLink } from "@mantine/core";

import { areaOn } from "../agents/features";
import { useAgentFeatures } from "../agents/useAgentFeatures";
import { agentEvaluationHref, agentHref, agentMonitoringHref, agentVersionsHref } from "../routes/links";
import type { Navigation } from "./navigation";

// Why: nesting is drawn by hand, not as a NavLink with children, because Mantine turns a click on such a NavLink into a toggle and these are all links
export function AppNavbar({ agentName, active }: Navigation) {
    const features = useAgentFeatures(agentName);
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
