import type { AgentDetail } from "../../agents/api";

type MonitoringSettings = NonNullable<AgentDetail["features"]["monitoring"]>;

const DEFAULT_CLOSE_AFTER = "30m";

export function monitoringSettingsText(settings: MonitoringSettings): string {
    const environments = settings.environments?.length ? settings.environments.join(", ") : "any";
    return `A conversation closes after ${settings.closeAfter ?? DEFAULT_CLOSE_AFTER} without a turn · environments: ${environments}`;
}
