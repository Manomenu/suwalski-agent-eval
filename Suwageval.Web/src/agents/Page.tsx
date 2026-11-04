import { Alert, Loader } from "@mantine/core";
import type { ReactNode } from "react";

import { isNotFound } from "../api/client";
import type { Loaded } from "../api/useRequest";

export function Page<T>({ state, notFound, children }: { state: Loaded<T>; notFound: string; children: (value: T) => ReactNode }) {
    if (state.kind === "loading") return <Loader aria-label="Loading" />;
    if (state.kind === "error") {
        const missing = isNotFound(state.error);
        return (
            <Alert color="red" title={missing ? "Not found" : "Could not load the page"}>
                {missing ? notFound : state.error.message}
            </Alert>
        );
    }
    return <>{children(state.value)}</>;
}
