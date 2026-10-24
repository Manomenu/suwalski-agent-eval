// Which item of the sidebar is lit for a page, as plain functions.
import type { Page } from "../routes/pageOf";

/** The items of the sidebar: the agents list, and the pages of the open agent. */
type NavItem = "agents" | "evaluation" | "monitoring" | "versions";

export interface Navigation {
    /** The agent shown under "Agents", or null when none is open (or not yet known). */
    agentName: string | null;
    /** The lit item, or null when none is. */
    active: NavItem | null;
}

/**
 * The agent and the lit item for a page. A run page and the comparison belong to Evaluation of their agent, a conversation's page to its
 * Monitoring, which only the data they load says (`agentOfPage`, reported through `useReportAgent`); a version page and the comparison of versions to Versions.
 */
export function navigationOf(page: Page, agentOfPage: string | null): Navigation {
    switch (page.kind) {
        case "agents":
            return { agentName: null, active: "agents" };
        case "agentEvaluation":
            return { agentName: page.name, active: "evaluation" };
        case "agentMonitoring":
            return { agentName: page.name, active: "monitoring" };
        case "agentVersions":
        case "version":
        case "versionCompare":
            return { agentName: page.name, active: "versions" };
        case "run":
        case "compare":
            return { agentName: agentOfPage, active: agentOfPage === null ? null : "evaluation" };
        case "conversation":
            return { agentName: agentOfPage, active: agentOfPage === null ? null : "monitoring" };
        case "unknown":
            return { agentName: null, active: null };
    }
}
