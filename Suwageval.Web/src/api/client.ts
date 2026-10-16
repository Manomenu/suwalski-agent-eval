/**
 * What every feature's HTTP calls share: the API's base path, `request`, and the error a
 * response turns into. The calls themselves live with their feature (`<feature>/api.ts`),
 * like the routes on the server; their types come from the generated `openapi.d.ts`.
 */

const BASE = import.meta.env.VITE_API_BASE ?? "/api";

/** The error a failed response carries: the server's `detail` when it sent one, and the status to tell answers apart by. */
export class ApiError extends Error {
    constructor(
        message: string,
        readonly status: number,
    ) {
        super(message);
    }
}

export async function toError(response: Response): Promise<ApiError> {
    const body = (await response.json().catch(() => null)) as { detail?: unknown } | null;
    const detail = body?.detail;
    return new ApiError(typeof detail === "string" ? detail : `${response.status} ${response.statusText}`, response.status);
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
