import { Alert, Anchor, Badge, Button, Checkbox, Group, Pagination, Paper, Stack, Switch, Table, Text, Title } from "@mantine/core";
import { useState } from "react";

import { formatTime, variantsGroupHref } from "../runs/results";
import { archiveRunGroups, deleteRunGroup, fetchRunGroupDeletion, type RunGroupSummary } from "./api";
import { DeleteModal } from "./DeleteModal";
import { groupSelection, pageOfGroups, visibleGroups } from "./groups";
import { HeaderHelp } from "../runs/HeaderHelp";
import { useGroupsPage } from "./useGroupsPage";

const GROUPS_HELP =
    "Runs started with `suwageval run --group <name>`: the runs of one sweep, typically one per model or configuration. Open a group to see its runs side by side as variants. A group's runs are experiments: no regression states, never a reference, not in the pass-rate trend. Archive a group to hide it; only an archived group can be deleted, with its runs.";
const GROUP_RUNS_HELP = "How many runs were started in the group (re-checks of them are not counted).";
const GROUP_VERSIONS_HELP = "How many different agent versions those runs used: the variants, when each run is of its own version.";
const GROUP_NEWEST_HELP = "When the group's newest run was queued.";
const GROUPS_TABLE_MIN_WIDTH = 500;

/** The agent's run groups: each opens the variants page of its runs; select some to archive them, see archived ones with "Show archived", restore or delete one. */
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
    const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
    const [showArchived, setShowArchived] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [notice, setNotice] = useState<string | null>(null);
    const [deleting, setDeleting] = useState(false);

    const [requestedPage, setRequestedPage] = useGroupsPage();
    const { groups: listed, page, pageCount } = pageOfGroups(visibleGroups(groups, showArchived), requestedPage);
    const { toArchive, toRestore, toDelete } = groupSelection(listed, selected);

    async function change(names: string[], archived: boolean) {
        setBusy(true);
        setError(null);
        setNotice(null);
        try {
            await archiveRunGroups(agentName, names, archived);
            setSelected(new Set());
            onChanged();
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : String(e));
        } finally {
            setBusy(false);
        }
    }

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
                        <Group justify="space-between">
                            <Group gap="sm">
                                <Button
                                    variant="light"
                                    disabled={toArchive.length === 0 || busy}
                                    onClick={() => {
                                        void change(toArchive, true);
                                    }}
                                >
                                    Archive selected groups
                                </Button>
                                {showArchived && (
                                    <Button
                                        variant="light"
                                        disabled={toRestore.length === 0 || busy}
                                        onClick={() => {
                                            void change(toRestore, false);
                                        }}
                                    >
                                        Unarchive selected groups
                                    </Button>
                                )}
                                {showArchived && (
                                    <Button
                                        color="red"
                                        variant="light"
                                        disabled={toDelete === null || busy}
                                        title={toDelete === null ? "Select one archived group to delete it." : undefined}
                                        onClick={() => {
                                            setDeleting(true);
                                        }}
                                    >
                                        Delete group
                                    </Button>
                                )}
                            </Group>
                            <Switch
                                label="Show archived groups"
                                checked={showArchived}
                                onChange={(event) => {
                                    setShowArchived(event.currentTarget.checked);
                                    setRequestedPage(1);
                                    setSelected(new Set());
                                }}
                            />
                        </Group>
                        {notice !== null && <Alert color="green">{notice}</Alert>}
                        {error !== null && (
                            <Alert color="red" title="Could not change the groups">
                                {error}
                            </Alert>
                        )}
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
                                                        const next = new Set(selected);
                                                        if (event.currentTarget.checked) next.add(group.name);
                                                        else next.delete(group.name);
                                                        setSelected(next);
                                                    }}
                                                />
                                            </Table.Td>
                                            <Table.Td className="wrap-key">
                                                <Group gap="xs">
                                                    <Anchor href={variantsGroupHref(group.name, agentName)}>{group.name}</Anchor>
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
                        {pageCount > 1 && (
                            <Pagination
                                total={pageCount}
                                value={page}
                                onChange={(next) => {
                                    setRequestedPage(next);
                                    setSelected(new Set());
                                }}
                            />
                        )}
                        {listed.length === 0 && (
                            <Text c="dimmed">All groups are archived — switch on Show archived groups to see them.</Text>
                        )}
                    </Stack>
                </Paper>
            )}
            {deleting && toDelete !== null && (
                <DeleteModal
                    noun="run group"
                    title="Delete this run group?"
                    subject={toDelete}
                    typedLabel="Type the group name to confirm"
                    expected={toDelete}
                    loadPreview={(signal) => fetchRunGroupDeletion(agentName, toDelete, signal)}
                    remove={() => deleteRunGroup(agentName, toDelete)}
                    onClose={() => {
                        setDeleting(false);
                    }}
                    onDeleted={() => {
                        setDeleting(false);
                        setSelected(new Set());
                        setNotice(`Deleted group ${toDelete} with its runs.`);
                        onChanged();
                    }}
                />
            )}
        </Stack>
    );
}
