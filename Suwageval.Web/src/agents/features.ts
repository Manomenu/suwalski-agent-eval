// Which areas of Suwageval an agent uses (`features:` in its agent.yaml, as pushed), as plain functions.
import type { AgentDetail } from "./api";

/** The areas an agent can switch on and off; each name is its key under `features:` in agent.yaml. */
export type Area = "evaluation" | "monitoring";

type Features = AgentDetail["features"];

/** Whether the area is on: evaluation unless switched off, monitoring only when its settings are there. */
export function areaOn(features: Features, area: Area): boolean {
    return area === "evaluation" ? features.evaluation !== false : features.monitoring != null;
}

/** What a page of a switched-off area says instead of its content. */
export function areaOffText(area: Area): string {
    const title = area === "evaluation" ? "Evaluation" : "Monitoring";
    return `${title} is off for this agent: features.${area} in its suwageval/agent.yaml (then suwageval push).`;
}
