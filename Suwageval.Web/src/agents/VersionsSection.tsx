import { Alert, Anchor, Badge, Button, Checkbox, Group, Paper, Stack, Switch, Table, Text, Title } from "@mantine/core";
import { useState } from "react";

import { formatTime, versionTitle } from "../runs/results";
import { archiveVersions, deleteVersion, fetchVersionDeletion, type AgentVersion } from "./api";
import { DeleteModal } from "./DeleteModal";
import { HeaderHelp } from "../runs/HeaderHelp";
import {
    firstPages,
    hasMorePages,
    compareVersionsTitle,
    selectionActions,
    versionCompareHref,
    versionHref,
    versionLabel,
    versionsToCompare,
    VERSION_CREATED_HELP,
    VERSION_ID_HELP,
    VERSION_LABEL_HELP,
    VERSIONS_SECTION_HELP,
    visibleVersions,
} from "./view";
import { versionToDelete } from "./deletion";

/** The agent's versions: select some to archive them (hidden), switch "Show archived" to see, restore or delete them (an archived version, with its runs). */
export function VersionsSection({
    agentName,
    versions,
    showArchived,
    onShowArchivedChange,
    onChanged,
}: {
    agentName: string;
    versions: AgentVersion[];
    showArchived: boolean;
    onShowArchivedChange: (show: boolean) => void;
    /** Called after the server archived or restored versions, so the page loads the list again. */
    onChanged: () => void;
}) {
    const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
    // How many pages of versions are shown ("Show more" adds one).
    const [pages, setPages] = useState(1);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [deleting, setDeleting] = useState(false);
    const [notice, setNotice] = useState<string | null>(null);

    const listed = visibleVersions(versions, showArchived);
    const { toArchive, toRestore } = selectionActions(listed, selected);
    const toCompare = versionsToCompare(listed, selected);
    const toDelete = versionToDelete(listed, selected);

    function toggle(id: string, checked: boolean) {
        const next = new Set(selected);
        if (checked) next.add(id);
        else next.delete(id);
        setSelected(next);
    }

    async function change(ids: string[], archived: boolean) {
        setBusy(true);
        setError(null);
        setNotice(null);
        try {
            await archiveVersions(agentName, ids, archived);
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
                <HeaderHelp label="Versions" help={VERSIONS_SECTION_HELP} />
            </Title>
            {versions.length === 0 ? (
                <Text c="dimmed">This agent has no versions yet — one is created on the first run.</Text>
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
                                    Archive selected
                                </Button>
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
                                {showArchived && (
                                    <Button
                                        variant="light"
                                        disabled={toRestore.length === 0 || busy}
                                        onClick={() => {
                                            void change(toRestore, false);
                                        }}
                                    >
                                        Unarchive selected
                                    </Button>
                                )}
                                {showArchived && (
                                    <Button
                                        color="red"
                                        variant="light"
                                        disabled={toDelete === null || busy}
                                        title={toDelete === null ? "Select one archived version to delete it." : undefined}
                                        onClick={() => {
                                            setDeleting(true);
                                        }}
                                    >
                                        Delete
                                    </Button>
                                )}
                            </Group>
                            <Switch
                                label="Show archived"
                                checked={showArchived}
                                onChange={(event) => {
                                    setPages(1);
                                    onShowArchivedChange(event.currentTarget.checked);
                                }}
                            />
                        </Group>
                        {notice !== null && <Alert color="green">{notice}</Alert>}
                        {error !== null && (
                            <Alert color="red" title="Could not change the versions">
                                {error}
                            </Alert>
                        )}
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
                                    {firstPages(listed, pages).map((version) => (
                                        <Table.Tr key={version.id}>
                                            <Table.Td>
                                                <Checkbox
                                                    aria-label={`Select version ${versionTitle(version.id, version.label)}`}
                                                    checked={selected.has(version.id)}
                                                    onChange={(event) => {
                                                        toggle(version.id, event.currentTarget.checked);
                                                    }}
                                                />
                                            </Table.Td>
                                            <Table.Td>
                                                <Group gap="xs">
                                                    {versionLabel(version)}
                                                    {version.archivedAt != null && (
                                                        <Badge color="gray" variant="light">
                                                            Archived
                                                        </Badge>
                                                    )}
                                                </Group>
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
                        {hasMorePages(listed, pages) && (
                            <Group justify="center">
                                <Button
                                    variant="light"
                                    onClick={() => {
                                        setPages(pages + 1);
                                    }}
                                >
                                    Show more
                                </Button>
                            </Group>
                        )}
                        {listed.length === 0 && <Text c="dimmed">All versions are archived — switch on Show archived to see them.</Text>}
                    </Stack>
                </Paper>
            )}
            {deleting && toDelete !== null && (
                <DeleteModal
                    noun="version"
                    title="Delete this version?"
                    subject={toDelete.label?.trim() ? toDelete.label : toDelete.id}
                    typedLabel="Type the version id to confirm"
                    expected={toDelete.id}
                    loadPreview={(signal) => fetchVersionDeletion(agentName, toDelete.id, signal)}
                    remove={() => deleteVersion(agentName, toDelete.id)}
                    onClose={() => {
                        setDeleting(false);
                    }}
                    onDeleted={() => {
                        setDeleting(false);
                        setSelected(new Set());
                        setNotice(`Deleted version ${versionTitle(toDelete.id, toDelete.label)} with its runs.`);
                        onChanged();
                    }}
                />
            )}
        </Stack>
    );
}
