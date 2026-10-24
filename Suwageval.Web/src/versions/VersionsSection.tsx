import { Anchor, Badge, Button, Checkbox, Chip, Group, Paper, Pill, Select, Stack, Table, Text, Title, Tooltip } from "@mantine/core";

import { type AgentVersion } from "../agents/api";
import { DeleteModal } from "../agents/DeleteModal";
import { HeaderHelp } from "../help/HeaderHelp";
import { ListPagination } from "../pagination/ListPagination";
import { pageOfItems } from "../pagination/pages";
import { usePageParam } from "../pagination/usePageParam";
import {
    compareVersionsTitle,
    versionLabel,
    versionsToCompare,
    VERSION_CONVERSATIONS_HELP,
    VERSION_CREATED_HELP,
    VERSION_LAST_CONVERSATION_HELP,
    VERSION_ID_HELP,
    VERSION_GROUP_RUNS_HELP,
    VERSION_LABEL_HELP,
    VERSION_RUNS_HELP,
    VERSIONS_SECTION_HELP,
} from "./view";
import { deletionSummary } from "../agents/deletion";
import { archiveSelection } from "../archive/archive";
import { ArchiveControls, type ArchiveLabels } from "../archive/ArchiveControls";
import { useArchiveList } from "../archive/useArchiveList";
import { currentText, environmentsOf, listedVersions, monitoringSummary, usagesOfValues, VERSION_USAGE_OPTIONS } from "./versionUsage";
import { versionCompareHref, versionHref, type VersionUsage } from "../routes/links";
import { formatTime } from "../format/time";
import { shortId, versionTitle } from "../format/names";
import { archiveVersions, deleteVersion, fetchVersionDeletion } from "./api";

/** How wide a usage chip's tooltip is. */
const USAGE_HELP_WIDTH = 280;

const LABELS: ArchiveLabels = {
    archive: "Archive selected",
    unarchive: "Unarchive selected",
    remove: "Delete",
    removeHint: "Select one archived version to delete it.",
    errorTitle: "Could not change the versions",
};

const VERSION_USAGE_HELP =
    "Where the versions are used; a version is listed when it has any of the chosen usages. Evaluation: it has runs outside a group (the default). Groups: it has runs in a group (run --group), such as a sweep's variants. Monitoring: it answered conversations of the deployed agent. Unused: it has no run and no conversation. Archived: the archived versions, which are listed only then.";

/** The agent's versions: select some to archive them (hidden), turn on the Archived usage chip to see, restore or delete them (an archived version, with its runs). */
export function VersionsSection({
    agentName,
    versions,
    usage,
    onUsageChange,
    versionIds,
    onVersionRemoved,
    environment,
    onEnvironmentChange,
    onChanged,
}: {
    agentName: string;
    versions: AgentVersion[];
    /** Where the listed versions are used: a version is listed when it has any of these (the archived ones only with `archived`). */
    usage: VersionUsage[];
    onUsageChange: (usage: VersionUsage[]) => void;
    /** The versions the list is narrowed to (none: no narrowing); each is shown as a chip that removes it. */
    versionIds: string[];
    onVersionRemoved: (id: string) => void;
    /** The monitored environment the monitoring usage and columns are narrowed to; null: every one. */
    environment: string | null;
    onEnvironmentChange: (environment: string | null) => void;
    /** Called after the server archived or restored versions, so the page loads the list again. */
    onChanged: () => void;
}) {
    const [requestedPage, setRequestedPage] = usePageParam("versionsPage");
    const list = useArchiveList((ids, archived) => archiveVersions(agentName, ids, archived), onChanged);
    const { selected } = list;

    const showArchived = usage.includes("archived");
    const shown = listedVersions(versions, usage, versionIds, environment);
    const environments = environmentsOf(versions);
    const monitored = environments.length > 0;
    const { items: listed, page } = pageOfItems(shown, requestedPage);
    const { toArchive, toRestore, toDelete } = archiveSelection(listed, selected, (version) => version.id);
    const toCompare = versionsToCompare(listed, selected);

    return (
        <Stack gap="sm">
            <Title order={3}>
                <HeaderHelp label="Versions" help={VERSIONS_SECTION_HELP} />
            </Title>
            {versions.length === 0 ? (
                <Text c="dimmed">This agent has no versions yet — one is created on the first run.</Text>
            ) : (
                <Paper withBorder p="md">
                    <Stack gap="sm">
                        <Stack gap={4}>
                            <HeaderHelp label="Usage" help={VERSION_USAGE_HELP} />
                            <Chip.Group
                                multiple
                                value={usage}
                                onChange={(values) => {
                                    setRequestedPage(1);
                                    list.clear();
                                    onUsageChange(usagesOfValues(values));
                                }}
                            >
                                <Group gap="xs">
                                    {VERSION_USAGE_OPTIONS.map((option) => (
                                        <Tooltip key={option.value} label={option.help} multiline w={USAGE_HELP_WIDTH}>
                                            <span>
                                                <Chip value={option.value}>{option.label}</Chip>
                                            </span>
                                        </Tooltip>
                                    ))}
                                </Group>
                            </Chip.Group>
                        </Stack>
                        {monitored && usage.includes("monitoring") && (
                            <Select
                                label="Environment"
                                placeholder="All environments"
                                data={environments}
                                value={environment}
                                onChange={(value) => {
                                    setRequestedPage(1);
                                    list.clear();
                                    onEnvironmentChange(value);
                                }}
                                clearable
                                maw={260}
                            />
                        )}
                        {versionIds.length > 0 && (
                            <Pill.Group>
                                {versionIds.map((id) => (
                                    <Pill
                                        key={id}
                                        withRemoveButton
                                        removeButtonProps={{ "aria-label": `Remove version ${shortId(id)}`, "aria-hidden": false }}
                                        onRemove={() => {
                                            setRequestedPage(1);
                                            list.clear();
                                            onVersionRemoved(id);
                                        }}
                                    >
                                        version {shortId(id)}
                                    </Pill>
                                ))}
                            </Pill.Group>
                        )}
                        <ArchiveControls
                            labels={LABELS}
                            toArchive={toArchive}
                            toRestore={toRestore}
                            canDelete={toDelete !== null}
                            busy={list.busy}
                            showArchived={showArchived}
                            notice={list.notice}
                            error={list.error}
                            extra={
                                <Button
                                    variant="light"
                                    disabled={toCompare === null}
                                    title={compareVersionsTitle(toCompare !== null)}
                                    onClick={() => {
                                        if (toCompare !== null)
                                            window.location.assign(versionCompareHref(agentName, toCompare[0], toCompare[1]));
                                    }}
                                >
                                    Compare versions
                                </Button>
                            }
                            onChange={(ids, archived) => {
                                void list.change(ids, archived);
                            }}
                            onDelete={() => {
                                list.setDeleting(true);
                            }}
                        />
                        <Table.ScrollContainer minWidth={500}>
                            <Table highlightOnHover>
                                <Table.Thead>
                                    <Table.Tr>
                                        <Table.Th />
                                        <Table.Th>
                                            <HeaderHelp label="Label" help={VERSION_LABEL_HELP} />
                                        </Table.Th>
                                        <Table.Th>
                                            <HeaderHelp label="Created" help={VERSION_CREATED_HELP} />
                                        </Table.Th>
                                        <Table.Th>
                                            <HeaderHelp label="Runs" help={VERSION_RUNS_HELP} />
                                        </Table.Th>
                                        <Table.Th>
                                            <HeaderHelp label="Group runs" help={VERSION_GROUP_RUNS_HELP} />
                                        </Table.Th>
                                        {monitored && (
                                            <>
                                                <Table.Th>
                                                    <HeaderHelp label="Conversations, 30 d" help={VERSION_CONVERSATIONS_HELP} />
                                                </Table.Th>
                                                <Table.Th>
                                                    <HeaderHelp label="Last conversation" help={VERSION_LAST_CONVERSATION_HELP} />
                                                </Table.Th>
                                            </>
                                        )}
                                        <Table.Th>
                                            <HeaderHelp label="Id" help={VERSION_ID_HELP} />
                                        </Table.Th>
                                    </Table.Tr>
                                </Table.Thead>
                                <Table.Tbody>
                                    {listed.map((version) => {
                                        const monitoring = monitoringSummary(version, environment);
                                        return (
                                            <Table.Tr key={version.id}>
                                                <Table.Td>
                                                    <Checkbox
                                                        aria-label={`Select version ${versionTitle(version.id, version.label)}`}
                                                        checked={selected.has(version.id)}
                                                        onChange={(event) => {
                                                            list.toggle(version.id, event.currentTarget.checked);
                                                        }}
                                                    />
                                                </Table.Td>
                                                <Table.Td>
                                                    <Stack gap={4} align="flex-start">
                                                        {versionLabel(version)}
                                                        {version.archivedAt != null && (
                                                            <Badge color="gray" variant="light">
                                                                Archived
                                                            </Badge>
                                                        )}
                                                        {version.groupRunsOnly && (
                                                            <Badge color="gray" variant="light">
                                                                Group runs only
                                                            </Badge>
                                                        )}
                                                        {monitoring.currentIn.length > 0 && (
                                                            <Badge color="green" variant="light">
                                                                {currentText(monitoring.currentIn, environments)}
                                                            </Badge>
                                                        )}
                                                        {monitoring.notEvaluated && (
                                                            <Badge color="yellow" variant="light">
                                                                Not evaluated
                                                            </Badge>
                                                        )}
                                                    </Stack>
                                                </Table.Td>
                                                <Table.Td>{formatTime(version.createdAt)}</Table.Td>
                                                <Table.Td>{version.runs}</Table.Td>
                                                <Table.Td>{version.groupRuns}</Table.Td>
                                                {monitored && (
                                                    <>
                                                        <Table.Td>{monitoring.conversations30Days}</Table.Td>
                                                        <Table.Td>
                                                            {monitoring.lastConversationAt === null
                                                                ? "—"
                                                                : formatTime(monitoring.lastConversationAt)}
                                                        </Table.Td>
                                                    </>
                                                )}
                                                <Table.Td>
                                                    <Anchor href={versionHref(agentName, version.id)}>{version.id}</Anchor>
                                                </Table.Td>
                                            </Table.Tr>
                                        );
                                    })}
                                </Table.Tbody>
                            </Table>
                        </Table.ScrollContainer>
                        <ListPagination
                            totalCount={shown.length}
                            page={page}
                            onChange={(next) => {
                                setRequestedPage(next);
                                list.clear();
                            }}
                        />
                        {listed.length === 0 && (
                            <Text c="dimmed">
                                No version to list — a version is listed when it has any of the chosen usages; archived versions only with
                                Archived, and with a version chip only that version.
                            </Text>
                        )}
                    </Stack>
                </Paper>
            )}
            {list.deleting && toDelete !== null && (
                <DeleteModal
                    noun="version"
                    title="Delete this version?"
                    subject={toDelete.label?.trim() ? toDelete.label : toDelete.id}
                    typedLabel="Type the version id to confirm"
                    expected={toDelete.id}
                    summary={deletionSummary}
                    loadPreview={(signal) => fetchVersionDeletion(agentName, toDelete.id, signal)}
                    remove={() => deleteVersion(agentName, toDelete.id)}
                    onClose={() => {
                        list.setDeleting(false);
                    }}
                    onDeleted={() => {
                        list.deleted(`Deleted version ${versionTitle(toDelete.id, toDelete.label)} with its runs.`);
                    }}
                />
            )}
        </Stack>
    );
}
