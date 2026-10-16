import { Alert, Badge, Container, Group, Loader, Stack, Text, Title } from "@mantine/core";

import { ScenarioCard } from "./ScenarioCard";
import { formatTime, isActive, isRunNotFound, num, progress, statusColor, statusLabel } from "./results";
import { useRunResults } from "./useRunResults";

/** One run's results at `/runs/<id>`: the header, then a card per scenario. */
export function RunPage({ runId }: { runId: string }) {
    const state = useRunResults(runId);

    if (state.kind === "loading") {
        return (
            <Container py="xl">
                <Loader aria-label="Ładuję wyniki" />
            </Container>
        );
    }
    if (state.kind === "error") {
        const notFound = isRunNotFound(state.error);
        return (
            <Container py="xl">
                <Alert color="red" title={notFound ? "Nie znaleziono runu" : "Nie udało się wczytać wyników"}>
                    {notFound ? `Nie ma runu ${runId}.` : state.error.message}
                </Alert>
            </Container>
        );
    }

    const { run, scenarios } = state.results;
    return (
        <Container py="xl" size="lg">
            <Stack gap="md">
                <Group gap="sm">
                    <Title order={2}>Run {run.agent}</Title>
                    <Badge size="lg" color={statusColor(run.status)}>
                        {statusLabel(run.status)}
                    </Badge>
                    {isActive(run.status) && <Loader size="sm" aria-label="Run trwa" />}
                </Group>
                <Text c="dimmed">
                    id {run.id} · wersja {run.agentVersionId}
                    {run.tag ? ` · tag ${run.tag}` : ""} · {run.endpointUrl}
                </Text>
                <Group gap="lg">
                    <Text>Próbki: {progress(run)}</Text>
                    <Text c="green">Checki zaliczone: {num(run.checksPassed)}</Text>
                    <Text c={num(run.checksFailed) > 0 ? "red" : "dimmed"}>Checki niezaliczone: {num(run.checksFailed)}</Text>
                </Group>
                <Text c="dimmed" size="sm">
                    utworzony {formatTime(run.createdAt)} · start {formatTime(run.startedAt)} · koniec {formatTime(run.finishedAt)}
                </Text>
                {run.contractChangedDuringRun === true && (
                    <Alert color="yellow" title="Kontrakt agenta zmienił się w trakcie runu">
                        Część próbek może nie pochodzić z tej wersji.
                    </Alert>
                )}
                {run.error && (
                    <Alert color="red" title="Run nie powiódł się">
                        {run.error}
                    </Alert>
                )}
                {scenarios.map((scenario) => (
                    <ScenarioCard key={scenario.key} scenario={scenario} />
                ))}
            </Stack>
        </Container>
    );
}
