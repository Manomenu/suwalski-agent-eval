import { ActionIcon, Button, CopyButton, Drawer, Group, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";

import { HighlightedYaml } from "../checks/HighlightedYaml";

const DEFINITION_DRAWER_WIDTH = 640;

/** A `{ }` button that opens the scenario as a scenario file, as the run used it, in a drawer with a copy button. Nothing shows until it is clicked. */
export function ScenarioDefinitionButton({ scenarioKey, revision, yaml }: { scenarioKey: string; revision: number; yaml: string }) {
    const [opened, { open, close }] = useDisclosure(false);
    return (
        <>
            <ActionIcon variant="subtle" size="sm" aria-label={`Show definition of scenario ${scenarioKey}`} onClick={open}>
                {"{ }"}
            </ActionIcon>
            <Drawer opened={opened} onClose={close} position="right" size={DEFINITION_DRAWER_WIDTH} title={scenarioKey}>
                <Stack gap="sm">
                    <Group justify="space-between">
                        <Text size="sm" c="dimmed">
                            {`Revision ${String(revision)}, checks as the run judged them`}
                        </Text>
                        <CopyButton value={yaml}>
                            {({ copied, copy }) => (
                                <Button variant="light" size="xs" onClick={copy}>
                                    {copied ? "Copied" : "Copy YAML"}
                                </Button>
                            )}
                        </CopyButton>
                    </Group>
                    <HighlightedYaml yaml={yaml} />
                </Stack>
            </Drawer>
        </>
    );
}
