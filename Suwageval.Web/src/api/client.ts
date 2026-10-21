/**
 * What every feature's HTTP calls share: the API's base path, `request`, and the error a
 * response turns into. The calls themselves live with their feature (`<feature>/api.ts`),
 * like the routes on the server; their types come from the generated `openapi.d.ts`.
 */

const BASE = import.meta.env.VITE_API_BASE ?? "/api";

/** The error a failed response carries: the server's `detail` (or, for a validation problem, its field messages), and the status to tell answers apart by. */
export class ApiError extends Error {
    constructor(
        message: string,
        readonly status: number,
    ) {
        super(message);
    }
}

/** The server's 404 — the only error the pages tell apart. */
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

/** A JSON call to the API: the parsed body, or the error the response carries. */
export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await fetch(`${BASE}${path}`, {
        ...init,
        headers: init.body === undefined ? {} : { "content-type": "application/json" },
    });
    if (!response.ok) throw await toError(response);
    // An answer with no body (204, or a bare 202) is `undefined`.
    const body = await response.text();
    return (body === "" ? undefined : JSON.parse(body)) as T;
}
