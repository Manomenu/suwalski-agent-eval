import { Anchor, Group, Paper, Stack, Text, Title, Tooltip } from "@mantine/core";

import { UNSTABLE_EXPLANATION, UnstableCheckIcon } from "../checks/UnstableCheckIcon";
import { ListPagination } from "../pagination/ListPagination";
import { pageOfItems } from "../pagination/pages";
import { usePageParam } from "../pagination/usePageParam";
import { passedOfRuns, UNSTABLE_CARD_RULE } from "./stability";
import { runHref } from "../routes/links";
import { versionTitle } from "../format/names";
import { type UnstableCheck } from "./api";
import { UNSTABLE_CARD_PAGE_SIZE } from "./stability";

const HEADING_HELP_WIDTH = 340;

/**
 * The checks that passed in some runs of one version and failed in others, so a failure there may be noise. Nothing at all
 * when there are none. Each row opens the newest run that failed the check.
 */
export function UnstableChecksCard({ checks }: { checks: UnstableCheck[] }) {
    const [requestedPage, setRequestedPage] = usePageParam("unstablePage");
    if (checks.length === 0) return null;
    const { items: listed, page } = pageOfItems(checks, requestedPage, UNSTABLE_CARD_PAGE_SIZE);
    return (
        <Paper withBorder p="md">
            <Stack gap="sm">
                {/* The heading and its icon explain on hover what an unstable check is and when a row is listed. */}
                <Tooltip
                    label={
                        <Stack gap="xs">
                            <Text size="sm">{UNSTABLE_EXPLANATION}</Text>
                            <Text size="sm">{UNSTABLE_CARD_RULE}</Text>
                        </Stack>
                    }
                    multiline
                    w={HEADING_HELP_WIDTH}
                >
                    <Group gap="xs" align="center" wrap="nowrap" style={{ cursor: "help", alignSelf: "flex-start" }}>
                        <UnstableCheckIcon />
                        <Title order={3} td="underline" style={{ textDecorationStyle: "dotted", textDecorationThickness: 1 }}>
                            Unstable checks
                        </Title>
                    </Group>
                </Tooltip>
                <Text c="dimmed" size="sm">
                    Passed in some runs of a version and failed in others.
                </Text>
                {listed.map((check) => (
                    <Group
                        key={JSON.stringify([check.agentVersionId, check.scenarioKey, check.checkKey, check.checkRevision])}
                        justify="space-between"
                        wrap="nowrap"
                        align="baseline"
                    >
                        <Anchor href={runHref(check.newestFailedRunId)} className="wrap-key">
                            {`${check.scenarioKey} / ${check.checkKey}`}
                        </Anchor>
                        <Group gap="xs" wrap="nowrap">
                            <Text fw={600}>{passedOfRuns(check)}</Text>
                            <Text c="dimmed" size="sm">
                                {versionTitle(check.agentVersionId, check.versionLabel)}
                            </Text>
                        </Group>
                    </Group>
                ))}
                <ListPagination totalCount={checks.length} page={page} pageSize={UNSTABLE_CARD_PAGE_SIZE} onChange={setRequestedPage} />
            </Stack>
        </Paper>
    );
}
