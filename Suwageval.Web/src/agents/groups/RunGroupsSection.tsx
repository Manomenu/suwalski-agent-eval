import { Anchor, Badge, Checkbox, Group, Paper, Stack, Table, Text, Title } from "@mantine/core";
import { useState } from "react";

import { DeleteModal } from "../DeleteModal";
import { deletionSummary } from "../deletion";
import { archiveSelection, visibleItems } from "../archive";
import { ArchiveControls, type ArchiveLabels } from "../ArchiveControls";
import { useArchiveList } from "../useArchiveList";
import { HeaderHelp } from "../../help/HeaderHelp";
import { ListPagination } from "../../pagination/ListPagination";
import { pageOfItems } from "../../pagination/pages";
import { usePageParam } from "../../pagination/usePageParam";
import { compareGroupHref } from "../../routes/links";
import { formatTime } from "../../format/time";
import { archiveRunGroups, deleteRunGroup, fetchRunGroupDeletion, type RunGroupSummary } from "./api";

const GROUPS_HELP =
    "Runs started with `suwageval run --group <name>`: the runs of one sweep, typically one per model or configuration. Open a group to see its runs side by side as variants. A group's runs are experiments: no regression states, never a reference, not in the pass-rate trend. Archive a group to hide it; only an archived group can be deleted, with its runs.";
const GROUP_RUNS_HELP = "How many runs were started in the group (re-checks of them are not counted).";
const GROUP_VERSIONS_HELP = "How many different agent versions those runs used: the variants, when each run is of its own version.";
const GROUP_NEWEST_HELP = "When the group's newest run was queued.";
const GROUPS_TABLE_MIN_WIDTH = 500;
const LABELS: ArchiveLabels = {
    archive: "Archive selected groups",
    unarchive: "Unarchive selected groups",
    remove: "Delete group",
    removeHint: "Select one archived group to delete it.",
    showArchived: "Show archived groups",
    errorTitle: "Could not change the groups",
};

/** The agent's run groups: each opens the compare page of its runs; select some to archive them, see archived ones with "Show archived", restore or delete one. */
export function RunGroupsSection({
    agentName,
    groups,
    onChanged,
}: {
    agentName: string;
    groups: RunGroupSummary[];
    /** Called after the server archived, restored or deleted groups, so the page loads its lists again. */
    onChanged: () => void;
}) {
    const [showArchived, setShowArchived] = useState(false);
    const [requestedPage, setRequestedPage] = usePageParam("groupsPage");
    const list = useArchiveList((names, archived) => archiveRunGroups(agentName, names, archived), onChanged);
    const { selected } = list;

    const shown = visibleItems(groups, showArchived);
    const { items: listed, page } = pageOfItems(shown, requestedPage);
    const { toArchive, toRestore, toDelete: groupToDelete } = archiveSelection(listed, selected, (group) => group.name);
    const toDelete = groupToDelete?.name ?? null;

    return (
        <Stack gap="sm">
            <Title order={3}>
                <HeaderHelp label="Groups" help={GROUPS_HELP} />
            </Title>
            {groups.length === 0 ? (
                <Text c="dimmed">
                    No run is in a group yet — start runs with <code>suwageval run --group &lt;name&gt;</code> to compare them side by side
                    here.
                </Text>
            ) : (
                <Paper withBorder p="md">
                    <Stack gap="sm">
                        <ArchiveControls
                            labels={LABELS}
                            toArchive={toArchive}
                            toRestore={toRestore}
                            canDelete={toDelete !== null}
                            busy={list.busy}
                            showArchived={showArchived}
                            notice={list.notice}
                            error={list.error}
                            onChange={(names, archived) => {
                                void list.change(names, archived);
                            }}
                            onDelete={() => {
                                list.setDeleting(true);
                            }}
                            onShowArchivedChange={(show) => {
                                setShowArchived(show);
                                setRequestedPage(1);
                                list.clear();
                            }}
                        />
                        <Table.ScrollContainer minWidth={GROUPS_TABLE_MIN_WIDTH}>
                            <Table highlightOnHover>
                                <Table.Thead>
                                    <Table.Tr>
                                        <Table.Th />
                                        <Table.Th>Group</Table.Th>
                                        <Table.Th>
                                            <HeaderHelp label="Runs" help={GROUP_RUNS_HELP} />
                                        </Table.Th>
                                        <Table.Th>
                                            <HeaderHelp label="Versions" help={GROUP_VERSIONS_HELP} />
                                        </Table.Th>
                                        <Table.Th>
                                            <HeaderHelp label="Newest run" help={GROUP_NEWEST_HELP} />
                                        </Table.Th>
                                    </Table.Tr>
                                </Table.Thead>
                                <Table.Tbody>
                                    {listed.map((group) => (
                                        <Table.Tr key={group.name}>
                                            <Table.Td>
                                                <Checkbox
                                                    aria-label={`Select group ${group.name}`}
                                                    checked={selected.has(group.name)}
                                                    onChange={(event) => {
                                                        list.toggle(group.name, event.currentTarget.checked);
                                                    }}
                                                />
                                            </Table.Td>
                                            <Table.Td className="wrap-key">
                                                <Group gap="xs">
                                                    <Anchor href={compareGroupHref(group.name, agentName)}>{group.name}</Anchor>
                                                    {group.archivedAt != null && (
                                                        <Badge color="gray" variant="light">
                                                            Archived
                                                        </Badge>
                                                    )}
                                                </Group>
                                            </Table.Td>
                                            <Table.Td>{group.runs}</Table.Td>
                                            <Table.Td>{group.versions}</Table.Td>
                                            <Table.Td>{formatTime(group.newestRunAt)}</Table.Td>
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
                            <Text c="dimmed">All groups are archived — switch on Show archived groups to see them.</Text>
                        )}
                    </Stack>
                </Paper>
            )}
            {list.deleting && toDelete !== null && (
                <DeleteModal
                    noun="run group"
                    title="Delete this run group?"
                    subject={toDelete}
                    typedLabel="Type the group name to confirm"
                    expected={toDelete}
                    summary={deletionSummary}
                    loadPreview={(signal) => fetchRunGroupDeletion(agentName, toDelete, signal)}
                    remove={() => deleteRunGroup(agentName, toDelete)}
                    onClose={() => {
                        list.setDeleting(false);
                    }}
                    onDeleted={() => {
                        list.deleted(`Deleted group ${toDelete} with its runs.`);
                    }}
                />
            )}
        </Stack>
    );
}
