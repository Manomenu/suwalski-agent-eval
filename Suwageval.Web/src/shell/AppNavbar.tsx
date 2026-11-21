import { NavLink } from "@maniumek/suwfrontkit";
import { IconChartLine, IconHeartbeat, IconHistory, IconRobot, IconUsersGroup } from "@tabler/icons-react";

import { areaOn } from "../agents/features";
import { useAgentFeatures } from "../agents/useAgentFeatures";
import { agentEvaluationHref, agentHref, agentMonitoringHref, agentVersionsHref } from "../routes/links";
import type { Navigation } from "./navigation";

export function AppNavbar({ agentName, active }: Navigation) {
    const features = useAgentFeatures(agentName);
    const evaluation = features === null || areaOn(features, "evaluation");
    const monitoring = features !== null && areaOn(features, "monitoring");
    return (
        <nav aria-label="Main" className="flex flex-col gap-1">
            <NavLink href="/" icon={<IconUsersGroup />} label="Agents" current={active === "agents"} />
            {agentName !== null && (
                <>
                    <NavLink href={agentHref(agentName)} icon={<IconRobot />} label={agentName} current={false} />
                    <NavLink href={agentVersionsHref(agentName)} icon={<IconHistory />} label="Versions" current={active === "versions"} />
                    {evaluation && (
                        <NavLink
                            href={agentEvaluationHref(agentName)}
                            icon={<IconChartLine />}
                            label="Evaluation"
                            current={active === "evaluation"}
                        />
                    )}
                    {monitoring && (
                        <NavLink
                            href={agentMonitoringHref(agentName)}
                            icon={<IconHeartbeat />}
                            label="Monitoring"
                            current={active === "monitoring"}
                        />
                    )}
                </>
            )}
        </nav>
    );
}
