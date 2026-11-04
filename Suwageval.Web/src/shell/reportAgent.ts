import { createContext, useContext, useEffect } from "react";

export const ReportAgentContext = createContext<(agent: string | null) => void>(() => undefined);

export function useReportAgent(agent: string | null): void {
    const report = useContext(ReportAgentContext);
    useEffect(() => {
        report(agent);
        return () => {
            report(null);
        };
    }, [agent, report]);
}
