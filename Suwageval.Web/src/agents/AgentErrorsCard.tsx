import { Anchor, Group, Paper, Stack, Text, Title, Tooltip } from "@mantine/core";

import { runHref, versionTitle } from "../runs/results";
import { AGENT_ERRORS_EXPLANATION, AGENT_ERRORS_PAGE_SIZE, errorsOfSamples, shortError } from "./agentErrors";
import type { AgentError } from "./api";
import { HeaderHelp } from "../runs/HeaderHelp";
import { ListPagination } from "../pagination/ListPagination";
import { pageOfItems } from "../pagination/pages";
import { usePageParam } from "../pagination/usePageParam";

const ERROR_TOOLTIP_WIDTH = 340;

/**
 * The scenarios where the agent gave no answer in some samples, per version, worst first. Nothing at all when there are none. Each
 * row opens the newest run in which the scenario had an error and shows that error (the full text on hover).
 */
export function AgentErrorsCard({ errors }: { errors: AgentError[] }) {
    const [requestedPage, setRequestedPage] = usePageParam("errorsPage");
    if (errors.length === 0) return null;
    const { items: listed, page } = pageOfItems(errors, requestedPage, AGENT_ERRORS_PAGE_SIZE);
    return (
        <Paper withBorder p="md">
            <Stack gap="sm">
                <Title order={3}>
                    <HeaderHelp label="Agent errors" help={AGENT_ERRORS_EXPLANATION} />
                </Title>
                {listed.map((error) => (
                    <Stack key={JSON.stringify([error.agentVersionId, error.scenarioKey])} gap={2}>
                        <Group justify="space-between" wrap="nowrap" align="baseline">
                            <Anchor href={runHref(error.newestErrorRunId)} className="wrap-key">
                                {error.scenarioKey}
                            </Anchor>
                            <Group gap="xs" wrap="nowrap">
                                <Text fw={600}>{errorsOfSamples(error)}</Text>
                                <Text c="dimmed" size="sm">
                                    {versionTitle(error.agentVersionId, error.versionLabel)}
                                </Text>
                            </Group>
                        </Group>
                        <Tooltip label={error.newestError} multiline w={ERROR_TOOLTIP_WIDTH} disabled={error.newestError === ""}>
                            <Text c="dimmed" size="sm">
                                {shortError(error.newestError)}
                            </Text>
                        </Tooltip>
                    </Stack>
                ))}
                <ListPagination totalCount={errors.length} page={page} pageSize={AGENT_ERRORS_PAGE_SIZE} onChange={setRequestedPage} />
            </Stack>
        </Paper>
    );
}
