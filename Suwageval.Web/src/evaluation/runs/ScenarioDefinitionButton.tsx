import { ActionIcon, Button, CopyButton, Drawer, Group, Stack, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";

import { HighlightedYaml } from "../checks/HighlightedYaml";

const DEFINITION_DRAWER_WIDTH = 640;

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
                            {`Revision ${String(revision)}`}
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
