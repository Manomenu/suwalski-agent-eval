import { Button, CopyButton, Dialog } from "@maniumek/suwfrontkit";
import { useState } from "react";

import { HighlightedYaml } from "../checks/HighlightedYaml";

export function ScenarioDefinitionButton({ scenarioKey, revision, yaml }: { scenarioKey: string; revision: number; yaml: string }) {
    const [opened, setOpened] = useState(false);
    return (
        <>
            <Button
                variant="ghost"
                size="icon"
                aria-label={`Show definition of scenario ${scenarioKey}`}
                onClick={() => {
                    setOpened(true);
                }}
            >
                {"{ }"}
            </Button>
            <Dialog open={opened} onOpenChange={setOpened} title={scenarioKey} width="wide">
                <div className="flex flex-col gap-3">
                    <div className="flex flex-wrap items-center justify-between gap-4">
                        <p className="text-sm text-muted-foreground">{`Revision ${String(revision)}`}</p>
                        <CopyButton value={yaml} label="Copy YAML" copiedLabel="Copied" variant="outline" size="xs" />
                    </div>
                    <HighlightedYaml yaml={yaml} />
                </div>
            </Dialog>
        </>
    );
}
