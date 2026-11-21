import { Button, Dialog } from "@maniumek/suwfrontkit";
import { useState } from "react";

import type { CheckDefinition } from "./api";
import { definitionSummary, definitionYaml, revisionNote } from "./definition";
import { HighlightedYaml } from "./HighlightedYaml";

export function CheckDefinitionButton({
    definition,
    revision,
    latestRevision,
}: {
    definition: CheckDefinition;
    revision: number;
    latestRevision: number;
}) {
    const [opened, setOpened] = useState(false);
    const note = revisionNote(revision, latestRevision);
    return (
        <>
            <Button
                variant="ghost"
                size="icon"
                aria-label={`Show definition of ${definition.key}`}
                onClick={() => {
                    setOpened(true);
                }}
            >
                {"{ }"}
            </Button>
            <Dialog open={opened} onOpenChange={setOpened} title={definition.key} width="wide">
                <div className="flex flex-col gap-3">
                    <p className="text-sm text-muted-foreground">{definitionSummary(definition)}</p>
                    {note !== null && <p className="text-sm text-warning">{note}</p>}
                    <HighlightedYaml yaml={definitionYaml(definition)} />
                </div>
            </Dialog>
        </>
    );
}
