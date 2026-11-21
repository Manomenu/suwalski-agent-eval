import { Anchor, Badge, formatDateTime, Paper, Table } from "@maniumek/suwfrontkit";

import { DeleteDialog } from "../../agents/DeleteDialog";
import { deletionSummary } from "../../agents/deletion";
import { archiveSelection, visibleItems } from "../../archive/archive";
import { ARCHIVED_GROUPS_PARAM } from "../../archive/archivedParam";
import { useArchivedParam } from "../../archive/useArchivedParam";
import { ArchiveControls, type ArchiveLabels } from "../../archive/ArchiveControls";
import { useArchiveList } from "../../archive/useArchiveList";
import { HeaderHelp } from "../../help/HeaderHelp";
import { ListPagination } from "../../pagination/ListPagination";
import { pageOfItems } from "../../pagination/pages";
import { usePageParam } from "../../pagination/usePageParam";
import { compareGroupHref } from "../../routes/links";
import { archiveRunGroups, deleteRunGroup, fetchRunGroupDeletion, runGroupKeys, type RunGroupSummary } from "./api";

const GROUPS_HELP =
    "Runs started with `suwageval run --group <name>`: the runs of one sweep, typically one per model or configuration. Open a group to see its runs side by side as variants. A group's runs are experiments: no regression states, never a reference, not in the pass-rate trend. Archive a group to hide it; only an archived group can be deleted, with its runs.";
const GROUP_RUNS_HELP = "How many runs were started in the group (re-checks of them are not counted).";
const GROUP_VERSIONS_HELP = "How many different agent versions those runs used: the variants, when each run is of its own version.";
const GROUP_NEWEST_HELP = "When the group's newest run was queued.";
const ARCHIVED_GROUPS_HELP =
    "Archived groups: hidden from the lists, kept with their runs; with this on they show and can be restored or deleted.";

const LABELS: ArchiveLabels = {
    group: "Run groups archive",
    archive: "Archive selected groups",
    unarchive: "Unarchive selected groups",
    remove: "Delete group",
    removeHint: "Select one archived group to delete it.",
    errorTitle: "Could not change the groups",
};

export function RunGroupsSection({
    agentName,
    groups,
    onChanged,
}: {
    agentName: string;
    groups: RunGroupSummary[];
    onChanged: () => void;
}) {
    const [showArchived, setShowArchived] = useArchivedParam(ARCHIVED_GROUPS_PARAM);
    const [requestedPage, setRequestedPage] = usePageParam("groupsPage");
    const list = useArchiveList((names, archived) => archiveRunGroups(agentName, names, archived), onChanged);
    const { selected } = list;

    const shown = visibleItems(groups, showArchived);
    const { items: listed, page } = pageOfItems(shown, requestedPage);
    const { toArchive, toRestore, toDelete: groupToDelete } = archiveSelection(listed, selected, (group) => group.name);
    const toDelete = groupToDelete?.name ?? null;

    return (
        <div className="flex flex-col gap-3">
            <h3 className="text-[22px] font-bold">
                <HeaderHelp label="Groups" help={GROUPS_HELP} />
            </h3>
            {groups.length === 0 ? (
                <p className="text-muted-foreground">
                    No run is in a group yet — start runs with <code>suwageval run --group &lt;name&gt;</code> to compare them side by side
                    here.
                </p>
            ) : (
                <Paper className="flex flex-col gap-3">
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
                        archivedChip={{
                            help: ARCHIVED_GROUPS_HELP,
                            onChange: (show) => {
                                setShowArchived(show);
                                setRequestedPage(1);
                                list.clear();
                            },
                        }}
                    />
                    <Table className="min-w-[500px]">
                        <thead>
                            <tr>
                                <th />
                                <th>Group</th>
                                <th>
                                    <HeaderHelp label="Runs" help={GROUP_RUNS_HELP} />
                                </th>
                                <th>
                                    <HeaderHelp label="Versions" help={GROUP_VERSIONS_HELP} />
                                </th>
                                <th>
                                    <HeaderHelp label="Newest run" help={GROUP_NEWEST_HELP} />
                                </th>
                            </tr>
                        </thead>
                        <tbody>
                            {listed.map((group) => (
                                <tr key={group.name}>
                                    <td>
                                        <input
                                            type="checkbox"
                                            className="size-4 accent-primary"
                                            aria-label={`Select group ${group.name}`}
                                            checked={selected.has(group.name)}
                                            onChange={(event) => {
                                                list.toggle(group.name, event.currentTarget.checked);
                                            }}
                                        />
                                    </td>
                                    <td className="wrap-key">
                                        <div className="flex flex-col items-start gap-1">
                                            <Anchor href={compareGroupHref(group.name, agentName)}>{group.name}</Anchor>
                                            {group.archivedAt != null && <Badge>Archived</Badge>}
                                        </div>
                                    </td>
                                    <td>{group.runs}</td>
                                    <td>{group.versions}</td>
                                    <td>{formatDateTime(group.newestRunAt)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </Table>
                    <ListPagination
                        totalCount={shown.length}
                        page={page}
                        onChange={(next) => {
                            setRequestedPage(next);
                            list.clear();
                        }}
                    />
                    {listed.length === 0 && (
                        <p className="text-muted-foreground">All groups are archived — turn on the Archived chip to see them.</p>
                    )}
                </Paper>
            )}
            {list.deleting && toDelete !== null && (
                <DeleteDialog
                    noun="run group"
                    title="Delete this run group?"
                    subject={toDelete}
                    typedLabel="Type the group name to confirm"
                    expected={toDelete}
                    previewKey={runGroupKeys.deletion(agentName, toDelete)}
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
        </div>
    );
}
