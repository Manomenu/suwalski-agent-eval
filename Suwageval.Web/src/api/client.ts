const BASE = import.meta.env.VITE_API_BASE ?? "/api";

export class ApiError extends Error {
    constructor(
        message: string,
        readonly status: number,
    ) {
        super(message);
    }
}

export function isNotFound(error: Error): boolean {
    return error instanceof ApiError && error.status === 404;
}

export async function toError(response: Response): Promise<ApiError> {
    const body = (await response.json().catch(() => null)) as { detail?: unknown; errors?: Record<string, string[]> } | null;
    const detail = body?.detail;
    const fieldMessages = Object.values(body?.errors ?? {}).flat();
    const message =
        typeof detail === "string"
            ? detail
            : fieldMessages.length > 0
              ? fieldMessages.join(" ")
              : `${response.status} ${response.statusText}`;
    return new ApiError(message, response.status);
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await fetch(`${BASE}${path}`, {
        ...init,
        headers: init.body === undefined ? {} : { "content-type": "application/json" },
    });
    if (!response.ok) throw await toError(response);
    const body = await response.text();
    return (body === "" ? undefined : JSON.parse(body)) as T;
}
