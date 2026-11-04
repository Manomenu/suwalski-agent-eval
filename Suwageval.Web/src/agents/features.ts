import type { AgentDetail } from "./api";

export type Area = "evaluation" | "monitoring";

type Features = AgentDetail["features"];

export function areaOn(features: Features, area: Area): boolean {
    return area === "evaluation" ? features.evaluation !== false : features.monitoring != null;
}

export function areaOffText(area: Area): string {
    const title = area === "evaluation" ? "Evaluation" : "Monitoring";
    return `${title} is off for this agent: features.${area} in its suwageval/agent.yaml (then suwageval push).`;
}
