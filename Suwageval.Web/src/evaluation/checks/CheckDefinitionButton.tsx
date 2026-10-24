import { ActionIcon, Drawer, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";

import type { CheckDefinition } from "./api";
import { definitionSummary, definitionYaml, revisionNote } from "./definition";
import { HighlightedYaml } from "./HighlightedYaml";

const DEFINITION_DRAWER_WIDTH = 640;

/** A `{ }` button that opens the check's definition, as the run used it, in a drawer. Nothing shows until it is clicked. */
export function CheckDefinitionButton({
    definition,
    revision,
    latestRevision,
}: {
    definition: CheckDefinition;
    revision: number;
    latestRevision: number;
}) {
    const [opened, { open, close }] = useDisclosure(false);
    const note = revisionNote(revision, latestRevision);
    return (
        <>
            <ActionIcon variant="subtle" size="sm" aria-label={`Show definition of ${definition.key}`} onClick={open}>
                {"{ }"}
            </ActionIcon>
            <Drawer opened={opened} onClose={close} position="right" size={DEFINITION_DRAWER_WIDTH} title={definition.key}>
                <Stack gap="sm">
                    <Text size="sm" c="dimmed">
                        {definitionSummary(definition)}
                    </Text>
                    {note !== null && (
                        <Text size="sm" c="yellow">
                            {note}
                        </Text>
                    )}
                    <HighlightedYaml yaml={definitionYaml(definition)} />
                </Stack>
            </Drawer>
        </>
    );
}
