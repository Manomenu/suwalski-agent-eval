// Which agent a page belongs to when its address does not say: a run page and the comparison know it only from the data they load.
// They report it here, and the sidebar (which `App` renders above the page) opens that agent's Evaluation. No extra request.
import { createContext, useContext, useEffect } from "react";

export const ReportAgentContext = createContext<(agent: string | null) => void>(() => undefined);

/** Reports the agent of the page (null while it is not known); the report is withdrawn when the page goes. */
export function useReportAgent(agent: string | null): void {
    const report = useContext(ReportAgentContext);
    useEffect(() => {
        report(agent);
        return () => {
            report(null);
        };
    }, [agent, report]);
}
