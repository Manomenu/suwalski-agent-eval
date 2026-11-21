import { API_BASE } from "../api/client";

export async function checkHealth(signal: AbortSignal): Promise<"up"> {
    const response = await fetch(`${API_BASE}/health`, { signal });
    if (!response.ok) throw new Error(`${String(response.status)} ${response.statusText}`.trimEnd());
    return "up";
}
