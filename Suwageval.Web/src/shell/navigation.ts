import type { Page } from "../routes/pageOf";

type NavItem = "agents" | "evaluation" | "monitoring" | "versions";

export interface Navigation {
    agentName: string | null;
    active: NavItem | null;
}

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
