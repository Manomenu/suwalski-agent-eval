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
    /** What a 404 says, e.g. "Nie ma agenta x." */
    notFound: string;
    children: (value: T) => ReactNode;
}) {
    if (state.kind === "loading") {
        return (
            <Container py="xl">
                <Loader aria-label="Ładuję" />
            </Container>
        );
    }
    if (state.kind === "error") {
        const missing = isNotFound(state.error);
        return (
            <Container py="xl">
                <Alert color="red" title={missing ? "Nie znaleziono" : "Nie udało się wczytać strony"}>
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
