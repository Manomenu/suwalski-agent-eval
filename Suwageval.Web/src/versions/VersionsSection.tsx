import { Anchor, Badge, Button, formatDateTime, Label, Paper, Select, Table, ToggleChip } from "@maniumek/suwfrontkit";
import { IconX } from "@tabler/icons-react";
import { useId } from "react";

import { agentKeys, type AgentVersion } from "../agents/api";
import { DeleteDialog } from "../agents/DeleteDialog";
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
import { shortId, versionTitle } from "../format/names";
import { archiveVersions, deleteVersion, fetchVersionDeletion } from "./api";

const LABELS: ArchiveLabels = {
    group: "Versions archive",
    archive: "Archive selected",
    unarchive: "Unarchive selected",
    remove: "Delete",
    removeHint: "Select one archived version to delete it.",
    errorTitle: "Could not change the versions",
};

const VERSION_USAGE_HELP =
    "Where the versions are used; a version is listed when it has any of the chosen usages. Evaluation: it has runs outside a group (the default). Groups: it has runs in a group (run --group), such as a sweep's variants. Monitoring: it answered conversations of the deployed agent. Unused: it has no run and no conversation. Archived: the archived versions, which are listed only then.";

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
    usage: VersionUsage[];
    onUsageChange: (usage: VersionUsage[]) => void;
    versionIds: string[];
    onVersionRemoved: (id: string) => void;
    environment: string | null;
    onEnvironmentChange: (environment: string | null) => void;
    onChanged: () => void;
}) {
    const environmentId = useId();
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
        <div className="flex flex-col gap-3">
            <h3 className="text-[22px] font-bold">
                <HeaderHelp label="Versions" help={VERSIONS_SECTION_HELP} />
            </h3>
            {versions.length === 0 ? (
                <p className="text-muted-foreground">This agent has no versions yet — one is created on the first run.</p>
            ) : (
                <Paper className="flex flex-col gap-3">
                    <div className="flex flex-col gap-1">
                        <HeaderHelp label="Usage" help={VERSION_USAGE_HELP} />
                        <div className="flex flex-wrap items-center gap-2.5">
                            {VERSION_USAGE_OPTIONS.map((option) => (
                                <ToggleChip
                                    key={option.value}
                                    help={option.help}
                                    pressed={usage.includes(option.value)}
                                    onPressedChange={(pressed) => {
                                        setRequestedPage(1);
                                        list.clear();
                                        onUsageChange(
                                            usagesOfValues(
                                                pressed ? [...usage, option.value] : usage.filter((value) => value !== option.value),
                                            ),
                                        );
                                    }}
                                >
                                    {option.label}
                                </ToggleChip>
                            ))}
                        </div>
                    </div>
                    {monitored && usage.includes("monitoring") && (
                        <div className="flex max-w-[260px] flex-col gap-1">
                            <Label htmlFor={environmentId}>Environment</Label>
                            <Select
                                id={environmentId}
                                value={environment ?? ""}
                                onChange={(event) => {
                                    setRequestedPage(1);
                                    list.clear();
                                    onEnvironmentChange(event.currentTarget.value === "" ? null : event.currentTarget.value);
                                }}
                            >
                                <option value="">All environments</option>
                                {environments.map((name) => (
                                    <option key={name} value={name}>
                                        {name}
                                    </option>
                                ))}
                            </Select>
                        </div>
                    )}
                    {versionIds.length > 0 && (
                        <div className="flex flex-wrap items-center gap-2.5">
                            {versionIds.map((id) => (
                                <Badge key={id} variant="neutral" className="gap-1">
                                    version {shortId(id)}
                                    <button
                                        type="button"
                                        aria-label={`Remove version ${shortId(id)}`}
                                        className="inline-flex cursor-pointer items-center"
                                        onClick={() => {
                                            setRequestedPage(1);
                                            list.clear();
                                            onVersionRemoved(id);
                                        }}
                                    >
                                        <IconX size={12} aria-hidden />
                                    </button>
                                </Badge>
                            ))}
                        </div>
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
                                variant="outline"
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
                    <Table className="min-w-[500px]">
                        <thead>
                            <tr>
                                <th />
                                <th>
                                    <HeaderHelp label="Label" help={VERSION_LABEL_HELP} />
                                </th>
                                <th>
                                    <HeaderHelp label="Created" help={VERSION_CREATED_HELP} />
                                </th>
                                <th>
                                    <HeaderHelp label="Runs" help={VERSION_RUNS_HELP} />
                                </th>
                                <th>
                                    <HeaderHelp label="Group runs" help={VERSION_GROUP_RUNS_HELP} />
                                </th>
                                {monitored && (
                                    <>
                                        <th>
                                            <HeaderHelp label="Conversations, 30 d" help={VERSION_CONVERSATIONS_HELP} />
                                        </th>
                                        <th>
                                            <HeaderHelp label="Last conversation" help={VERSION_LAST_CONVERSATION_HELP} />
                                        </th>
                                    </>
                                )}
                                <th>
                                    <HeaderHelp label="Id" help={VERSION_ID_HELP} />
                                </th>
                            </tr>
                        </thead>
                        <tbody>
                            {listed.map((version) => {
                                const monitoring = monitoringSummary(version, environment);
                                return (
                                    <tr key={version.id}>
                                        <td>
                                            <input
                                                type="checkbox"
                                                className="size-4 accent-primary"
                                                aria-label={`Select version ${versionTitle(version.id, version.label)}`}
                                                checked={selected.has(version.id)}
                                                onChange={(event) => {
                                                    list.toggle(version.id, event.currentTarget.checked);
                                                }}
                                            />
                                        </td>
                                        <td>
                                            <div className="flex flex-col items-start gap-1">
                                                {versionLabel(version)}
                                                {version.archivedAt != null && <Badge variant="neutral">Archived</Badge>}
                                                {monitoring.currentIn.length > 0 && (
                                                    <Badge variant="success">{currentText(monitoring.currentIn, environments)}</Badge>
                                                )}
                                                {monitoring.notEvaluated && <Badge variant="warning">Not evaluated</Badge>}
                                            </div>
                                        </td>
                                        <td>{formatDateTime(version.createdAt)}</td>
                                        <td>{version.runs}</td>
                                        <td>{version.groupRuns}</td>
                                        {monitored && (
                                            <>
                                                <td>{monitoring.conversations30Days}</td>
                                                <td>
                                                    {monitoring.lastConversationAt === null
                                                        ? "—"
                                                        : formatDateTime(monitoring.lastConversationAt)}
                                                </td>
                                            </>
                                        )}
                                        <td>
                                            <Anchor href={versionHref(agentName, version.id)}>{version.id}</Anchor>
                                        </td>
                                    </tr>
                                );
                            })}
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
                        <p className="text-muted-foreground">
                            No version to list — a version is listed when it has any of the chosen usages; archived versions only with
                            Archived, and with a version chip only that version.
                        </p>
                    )}
                </Paper>
            )}
            {list.deleting && toDelete !== null && (
                <DeleteDialog
                    noun="version"
                    title="Delete this version?"
                    subject={toDelete.label?.trim() ? toDelete.label : toDelete.id}
                    typedLabel="Type the version id to confirm"
                    expected={toDelete.id}
                    summary={deletionSummary}
                    previewKey={[...agentKeys.version(agentName, toDelete.id), "deletion"]}
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
        </div>
    );
}
