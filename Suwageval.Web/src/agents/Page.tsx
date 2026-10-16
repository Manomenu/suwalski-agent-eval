import { Alert, Container, Loader } from "@mantine/core";
import type { ReactNode } from "react";

import type { Loaded } from "./useLoaded";
import { isNotFound } from "./view";

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
    if (state.kind === "loading") {
        return (
            <Container py="xl">
                <Loader aria-label="Loading" />
            </Container>
        );
    }
    if (state.kind === "error") {
        const missing = isNotFound(state.error);
        return (
            <Container py="xl">
                <Alert color="red" title={missing ? "Not found" : "Could not load the page"}>
                    {missing ? notFound : state.error.message}
                </Alert>
            </Container>
        );
    }
    return (
        <Container py="xl" size="lg">
            {children(state.value)}
        </Container>
    );
}
