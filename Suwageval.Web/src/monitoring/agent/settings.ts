// The agent's monitoring settings (features.monitoring in its agent.yaml) in words.
import type { AgentDetail } from "../../agents/api";

type MonitoringSettings = NonNullable<AgentDetail["features"]["monitoring"]>;

/** The default of `closeAfter` (FeatureRules.DefaultCloseAfter on the server). */
const DEFAULT_CLOSE_AFTER = "30m";

/** "A conversation closes after 30m without a turn · environments: production, staging" (any environment when none are named). */
export function monitoringSettingsText(settings: MonitoringSettings): string {
    const environments = settings.environments?.length ? settings.environments.join(", ") : "any";
    return `A conversation closes after ${settings.closeAfter ?? DEFAULT_CLOSE_AFTER} without a turn · environments: ${environments}`;
}
