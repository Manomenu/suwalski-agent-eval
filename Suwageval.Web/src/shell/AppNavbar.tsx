import { NavTree, type NavItem2, type NavItem3 } from "@maniumek/suwfrontkit";
import { IconChartLine, IconHeartbeat, IconHistory, IconRobot, IconUsersGroup } from "@tabler/icons-react";

import { areaOn } from "../agents/features";
import { useAgentFeatures } from "../agents/useAgentFeatures";
import { agentEvaluationHref, agentHref, agentMonitoringHref, agentVersionsHref } from "../routes/links";
import type { Navigation } from "./navigation";

export function AppNavbar({ agentName, active }: Navigation) {
    const features = useAgentFeatures(agentName);
    const evaluation = features === null || areaOn(features, "evaluation");
    const monitoring = features !== null && areaOn(features, "monitoring");
    const areas = (name: string): NavItem3[] => [
        { href: agentVersionsHref(name), icon: <IconHistory />, label: "Versions", current: active === "versions" },
        ...(evaluation
            ? [{ href: agentEvaluationHref(name), icon: <IconChartLine />, label: "Evaluation", current: active === "evaluation" }]
            : []),
        ...(monitoring
            ? [{ href: agentMonitoringHref(name), icon: <IconHeartbeat />, label: "Monitoring", current: active === "monitoring" }]
            : []),
    ];
    const agent: NavItem2[] =
        agentName === null
            ? []
            : [{ href: agentHref(agentName), icon: <IconRobot />, label: agentName, current: false, items: areas(agentName) }];
    return (
        <nav aria-label="Main">
            <NavTree items={[{ href: "/", icon: <IconUsersGroup />, label: "Agents", current: active === "agents", items: agent }]} />
        </nav>
    );
}
