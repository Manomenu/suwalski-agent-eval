// This feature's HTTP calls, typed from the generated OpenAPI types. A comparison is made from the runs' own results (no
// endpoint of its own: the server has nothing to add to them), the agent's versions (label, model) and its scenarios (tags).
import { fetchVersions, type AgentVersion } from "../agents/api";
import { isNotFound } from "../agents/view";
import { request } from "../api/client";
import type { components } from "../api/openapi";
import { fetchRunResults, type RunResults } from "../runs/api";
import { DIFFERENT_AGENTS, sameAgent } from "./compare";

type ScenarioSummary = components["schemas"]["ScenarioSummary"];

export interface Comparison {
    /** One per run, in the order asked for. */
    runs: RunResults[];
    versions: AgentVersion[];
    /** Scenario key to its current tags. */
    tags: ReadonlyMap<string, readonly string[]>;
}

function fetchScenarios(agent: string, signal: AbortSignal): Promise<ScenarioSummary[]> {
    return request<ScenarioSummary[]>(`/agents/${encodeURIComponent(agent)}/scenarios`, { signal });
}

/** Everything the page shows; an `Error` with words for the user when the runs cannot be compared. */
export async function fetchComparison(runIds: string[], signal: AbortSignal): Promise<Comparison> {
    // All are asked at once; when several fail, the first in the order asked for is the one reported.
    const settled = await Promise.allSettled(runIds.map((id) => fetchRunResults(id, signal)));
    const runs: RunResults[] = [];
    settled.forEach((outcome, index) => {
        if (outcome.status === "fulfilled") {
            runs.push(outcome.value);
            return;
        }
        const reason: unknown = outcome.reason;
        throw reason instanceof Error && isNotFound(reason) ? new Error(`There is no run ${runIds[index]}.`) : reason;
    });
    if (!sameAgent(runs)) throw new Error(DIFFERENT_AGENTS);
    const agent = runs[0]?.run.agent ?? "";
    const [versions, scenarios] = await Promise.all([fetchVersions(agent, signal), fetchScenarios(agent, signal)]);
    return { runs, versions, tags: new Map(scenarios.map((scenario) => [scenario.key, scenario.tags])) };
}
