import { Anchor, Diff, Paper, formatDateTime } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";

import { type AgentVersion } from "../agents/api";
import { Page } from "../agents/Page";
import { agentHref, versionHref } from "../routes/links";
import { versionTitle } from "../format/names";
import { versionDiffQuery } from "./api";

function VersionCard({ name, role, version }: { name: string; role: string; version: AgentVersion }) {
    return (
        <Paper className="flex flex-col gap-1">
            <p className="text-sm text-muted-foreground">{role}</p>
            <Anchor href={versionHref(name, version.id)} className="font-semibold">
                {versionTitle(version.id, version.label)}
            </Anchor>
            <p className="text-sm">created {formatDateTime(version.createdAt)}</p>
            <p className="text-sm">models {version.modelNames.join(", ")}</p>
        </Paper>
    );
}

export function VersionComparePage({ name, a, b }: { name: string; a: string; b: string }) {
    const query = useQuery(versionDiffQuery(name, a, b));

    return (
        <Page query={query} notFound={`Agent ${name} has no version ${a} or ${b}.`}>
            {(diff) => (
                <div className="flex flex-col gap-4">
                    <Anchor href={agentHref(name)}>← {name}</Anchor>
                    <h2 className="text-[26px] leading-tight font-bold">Compare versions</h2>
                    <div className="grid grid-cols-2 items-stretch gap-4">
                        <VersionCard name={name} role="Base (older)" version={diff.from} />
                        <VersionCard name={name} role="Newer" version={diff.to} />
                    </div>
                    <h3 className="text-[22px] font-bold">Diff</h3>
                    <Diff before={diff.fromYaml} after={diff.toYaml} />
                </div>
            )}
        </Page>
    );
}
