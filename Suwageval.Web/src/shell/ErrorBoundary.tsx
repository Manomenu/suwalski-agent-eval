import { Alert, Anchor, Group, Text } from "@mantine/core";
import { Component, type ReactNode } from "react";

interface State {
    error: Error | null;
}

class Catcher extends Component<{ children: ReactNode }, State> {
    override state: State = { error: null };

    static getDerivedStateFromError(error: Error): State {
        return { error };
    }

    override render() {
        const { error } = this.state;
        if (error === null) return this.props.children;
        return (
            <Alert color="red" title="This page failed to open">
                <Text size="sm">{error.message}</Text>
                <Group gap="md" mt="xs">
                    <Anchor href={window.location.href}>Reload</Anchor>
                    <Anchor href="/">Back to the list of agents</Anchor>
                </Group>
            </Alert>
        );
    }
}

export function ErrorBoundary({ children }: { children: ReactNode }) {
    return <Catcher key={window.location.pathname}>{children}</Catcher>;
}
