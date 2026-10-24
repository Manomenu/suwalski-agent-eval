import { Anchor, Badge, Button, Checkbox, Paper, Select, Stack, Table, Text, Title } from "@mantine/core";

import { type AgentVersion } from "../api";
import { DeleteModal } from "../DeleteModal";
import { HeaderHelp } from "../../help/HeaderHelp";
import { ListPagination } from "../../pagination/ListPagination";
import { pageOfItems } from "../../pagination/pages";
import { usePageParam } from "../../pagination/usePageParam";
import {
    compareVersionsTitle,
    versionLabel,
    versionsToCompare,
    VERSION_CREATED_HELP,
    VERSION_ID_HELP,
    VERSION_LABEL_HELP,
    VERSIONS_SECTION_HELP,
} from "../view";
import { deletionSummary } from "../deletion";
import { archiveSelection, visibleItems } from "../archive";
import { ArchiveControls, type ArchiveLabels } from "../ArchiveControls";
import { useArchiveList } from "../useArchiveList";
import { inVersionGrouping, VERSION_GROUPING_OPTIONS, versionGroupingOfValue, type VersionGrouping } from "../versionGrouping";
import { versionCompareHref, versionHref } from "../../routes/links";
import { formatTime } from "../../format/time";
import { versionTitle } from "../../format/names";
import { archiveVersions, deleteVersion, fetchVersionDeletion } from "./api";

const LABELS: ArchiveLabels = {
    archive: "Archive selected",
    unarchive: "Unarchive selected",
    remove: "Delete",
    removeHint: "Select one archived version to delete it.",
    showArchived: "Show archived",
    errorTitle: "Could not change the versions",
};

const VERSION_GROUP_HELP =
    "Which versions to list by their runs (run --group): those with a run outside a group or with none (the default), those whose every run is in a group (experiments, such as a sweep's variants), or all. Archiving is separate.";
const GROUP_SELECT_MAX_WIDTH = 260;

/** The agent's versions: select some to archive them (hidden), switch "Show archived" to see, restore or delete them (an archived version, with its runs). */
export function VersionsSection({
    agentName,
    versions,
    showArchived,
    onShowArchivedChange,
    grouping,
    onGroupingChange,
    onChanged,
}: {
    agentName: string;
    versions: AgentVersion[];
    showArchived: boolean;
    onShowArchivedChange: (show: boolean) => void;
    /** Which versions are listed by whether their runs are in groups: those in no group are the default. */
    grouping: VersionGrouping;
    onGroupingChange: (grouping: VersionGrouping) => void;
    /** Called after the server archived or restored versions, so the page loads the list again. */
    onChanged: () => void;
}) {
    const [requestedPage, setRequestedPage] = usePageParam("versionsPage");
    const list = useArchiveList((ids, archived) => archiveVersions(agentName, ids, archived), onChanged);
    const { selected } = list;

    const shown = visibleItems(versions, showArchived).filter((version) => inVersionGrouping(version, grouping));
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
                        <Select
                            label={<HeaderHelp label="Version group" help={VERSION_GROUP_HELP} />}
                            data={VERSION_GROUPING_OPTIONS}
                            value={grouping}
                            onChange={(value) => {
                                setRequestedPage(1);
                                list.clear();
                                onGroupingChange(versionGroupingOfValue(value));
                            }}
                            allowDeselect={false}
                            maw={GROUP_SELECT_MAX_WIDTH}
                        />
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
                            onShowArchivedChange={(show) => {
                                setRequestedPage(1);
                                list.clear();
                                onShowArchivedChange(show);
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
                                            <HeaderHelp label="Id" help={VERSION_ID_HELP} />
                                        </Table.Th>
                                    </Table.Tr>
                                </Table.Thead>
                                <Table.Tbody>
                                    {listed.map((version) => (
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
                                                </Stack>
                                            </Table.Td>
                                            <Table.Td>{formatTime(version.createdAt)}</Table.Td>
                                            <Table.Td>
                                                <Anchor href={versionHref(agentName, version.id)}>{version.id}</Anchor>
                                            </Table.Td>
                                        </Table.Tr>
                                    ))}
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
                                No version to list — archived versions are hidden unless Show archived is on, and versions with group runs
                                only unless the group is Any group or All versions.
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
