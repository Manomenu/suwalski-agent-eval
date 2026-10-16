import { Alert, Loader } from "@mantine/core";
import type { ReactNode } from "react";

import { isNotFound } from "../api/client";
import type { Loaded } from "./useLoaded";

/** The frame of a page that loads something: a spinner, the error (a 404 in plain words) or the content. */
export function Page<T>({
    state,
    notFound,
    children,
}: {
    state: Loaded<T>;
    /** What a 404 says, e.g. "There is no agent x." */
    notFound: string;
    children: (value: T) => ReactNode;
}) {
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
