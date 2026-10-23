import { Anchor, Badge, Group, List, Stack, Table, Text, Title, UnstyledButton } from "@mantine/core";
import { IconChevronDown, IconChevronRight } from "@tabler/icons-react";

import type { BaselineComparison, VariantCell, VariantRun, VariantScenario } from "./api";
import { ChangeNote } from "./ChangeNote";
import { changeBackground, checkCellChange, scenarioCellChange, type Change } from "./compare";
import { COLUMN_MIN_WIDTH, LABEL_COLUMN_WIDTH } from "./layout";
import {
    bestColumns,
    cellShare,
    cellStatus,
    cellText,
    checkMatrix,
    checkVerdictText,
    columnName,
    noAnswerRows,
    noAnswerText,
    type ScenarioSection,
} from "./variants";
import { ModelJudgedIcon } from "../checks/ModelJudgedIcon";
import { scenarioInRunHref } from "../routes/links";
import { shortId } from "../format/names";

const BEST_BACKGROUND = "var(--mantine-color-green-light)";

/** The scenario rows of one section (a tag's, or all): a compact cell per run, and a row that opens into the detail of the checks. */
export function ScenarioRows({
    section,
    columns,
    baseline,
    open,
    onToggle,
}: {
    section: ScenarioSection;
    columns: readonly VariantRun[];
    baseline: BaselineComparison | null;
    open: readonly string[];
    onToggle: (key: string) => void;
}) {
    return (
        <>
            {section.title !== null && (
                <Table.Tr>
                    <Table.Td colSpan={columns.length + 1} fz="xs" c="dimmed" fw={600} className="wrap-key">
                        {section.title}
                    </Table.Td>
                </Table.Tr>
            )}
            {section.scenarios.map((scenario) => {
                // With a baseline every cell is read by its change; without one the best column of the row is marked.
                const best = baseline === null ? bestColumns(scenario.cells.map(cellShare)) : new Set<number>();
                const baselineColumn = baseline === null ? -1 : columns.findIndex((column) => column.run.id === baseline.runId);
                const baselineCell = scenario.cells[baselineColumn] ?? null;
                const changes =
                    baseline === null
                        ? []
                        : scenario.cells.map((cell, column) => (column === baselineColumn ? null : scenarioCellChange(cell, baselineCell)));
                const expanded = open.includes(scenario.key);
                const detailId = `scenario-detail-${scenario.key}`;
                return (
                    <ScenarioRow
                        key={scenario.key}
                        scenario={scenario}
                        columns={columns}
                        best={best}
                        changes={changes}
                        baseline={baseline}
                        baselineColumn={baselineColumn}
                        expanded={expanded}
                        detailId={detailId}
                        onToggle={() => {
                            onToggle(scenario.key);
                        }}
                    />
                );
            })}
        </>
    );
}

function ScenarioRow({
    scenario,
    columns,
    best,
    changes,
    baseline,
    baselineColumn,
    expanded,
    detailId,
    onToggle,
}: {
    scenario: VariantScenario;
    columns: readonly VariantRun[];
    best: ReadonlySet<number>;
    changes: readonly (Change | null)[];
    baseline: BaselineComparison | null;
    baselineColumn: number;
    expanded: boolean;
    detailId: string;
    onToggle: () => void;
}) {
    return (
        <>
            <Table.Tr>
                <Table.Td className="wrap-key">
                    <UnstyledButton onClick={onToggle} aria-expanded={expanded} aria-controls={detailId} w="100%">
                        <Group gap={4} wrap="nowrap" align="flex-start">
                            {expanded ? <IconChevronDown size={16} aria-hidden /> : <IconChevronRight size={16} aria-hidden />}
                            <Text span inherit className="wrap-key">
                                {scenario.key}
                            </Text>
                        </Group>
                    </UnstyledButton>
                </Table.Td>
                {scenario.cells.map((cell, column) => (
                    <CellView key={columns[column]?.run.id} cell={cell} best={best.has(column)} change={changes[column] ?? null} />
                ))}
            </Table.Tr>
            {expanded && (
                <Table.Tr>
                    <Table.Td colSpan={columns.length + 1} id={detailId}>
                        <ScenarioDetail scenario={scenario} columns={columns} baseline={baseline} baselineColumn={baselineColumn} />
                    </Table.Td>
                </Table.Tr>
            )}
        </>
    );
}

/** A run's result for a scenario at a glance: a badge that says it in words, and the count behind it. */
function CellView({ cell, best, change }: { cell: VariantCell | null; best: boolean; change: Change | null }) {
    const status = cellStatus(cell);
    const noAnswer = noAnswerText(cell);
    return (
        <Table.Td bg={best ? BEST_BACKGROUND : changeBackground(change)} data-change={change ?? undefined}>
            {status === null ? (
                <Text size="sm">{cellText(cell)}</Text>
            ) : (
                <Stack gap={2} align="flex-start">
                    <Badge color={status.color} variant={status.color === "red" ? "filled" : "light"}>
                        {status.label}
                    </Badge>
                    <Text size="xs" fw={best ? 700 : 400}>
                        {cellText(cell)}
                    </Text>
                    {noAnswer !== null && (
                        <Text size="xs" c="dimmed">
                            {noAnswer}
                        </Text>
                    )}
                    <ChangeNote change={change} />
                </Stack>
            )}
        </Table.Td>
    );
}

/** What is behind a scenario row: the checks by run, why the samples failed them, the samples nobody answered, and where to read more. */
function ScenarioDetail({
    scenario,
    columns,
    baseline,
    baselineColumn,
}: {
    scenario: VariantScenario;
    columns: readonly VariantRun[];
    baseline: BaselineComparison | null;
    baselineColumn: number;
}) {
    const matrix = checkMatrix(scenario.cells);
    const noAnswers = noAnswerRows(scenario.cells);
    return (
        <Stack gap="sm" py="xs">
            {matrix.length === 0 ? (
                <Text size="sm" c="dimmed">
                    No check ran for this scenario.
                </Text>
            ) : (
                <Table.ScrollContainer minWidth={LABEL_COLUMN_WIDTH + columns.length * COLUMN_MIN_WIDTH}>
                    <Table layout="fixed" verticalSpacing="xs" withColumnBorders aria-label={`Checks of ${scenario.key} by run`}>
                        <Table.Thead>
                            <Table.Tr>
                                <Table.Th w={LABEL_COLUMN_WIDTH}>Check</Table.Th>
                                {columns.map((column) => (
                                    <Table.Th key={column.run.id} miw={COLUMN_MIN_WIDTH} className="wrap-key">
                                        {columnName(column)}
                                    </Table.Th>
                                ))}
                            </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                            {matrix.map((row) => {
                                const compared = baseline?.checks.find(
                                    (candidate) => candidate.scenarioKey === scenario.key && candidate.checkKey === row.key,
                                );
                                const baselineCheck = row.checks[baselineColumn] ?? null;
                                return (
                                    <Table.Tr key={row.key}>
                                        <Table.Td className="wrap-key">
                                            <Group gap={4} wrap="nowrap" align="flex-start">
                                                {row.checks.some((check) => check?.judgedByModel === true) && <ModelJudgedIcon />}
                                                <Text span inherit>
                                                    {row.key}
                                                </Text>
                                            </Group>
                                        </Table.Td>
                                        {row.checks.map((check, column) => {
                                            const change =
                                                baseline === null || column === baselineColumn
                                                    ? null
                                                    : checkCellChange(check, baselineCheck, compared, column);
                                            return (
                                                <Table.Td
                                                    key={columns[column]?.run.id}
                                                    bg={changeBackground(change)}
                                                    data-change={change ?? undefined}
                                                >
                                                    {check === null ? (
                                                        <Text size="sm" c="dimmed">
                                                            —
                                                        </Text>
                                                    ) : (
                                                        <Stack gap={2} align="flex-start">
                                                            <Badge
                                                                color={check.passed ? "green" : "red"}
                                                                variant={check.passed ? "light" : "filled"}
                                                            >
                                                                {checkVerdictText(check)}
                                                            </Badge>
                                                            <List size="xs" listStyleType="none">
                                                                {check.failures.map((failure) => (
                                                                    <List.Item key={failure.sample}>
                                                                        <Text span inherit className="wrap-key">
                                                                            {check.samples > 1 ? `sample ${String(failure.sample)}: ` : ""}
                                                                            {failure.message}
                                                                        </Text>
                                                                    </List.Item>
                                                                ))}
                                                            </List>
                                                        </Stack>
                                                    )}
                                                    <ChangeNote change={change} />
                                                </Table.Td>
                                            );
                                        })}
                                    </Table.Tr>
                                );
                            })}
                        </Table.Tbody>
                    </Table>
                </Table.ScrollContainer>
            )}
            {noAnswers.length > 0 && (
                <Stack gap={2}>
                    <Title order={5}>Samples without an answer</Title>
                    <List size="sm" listStyleType="none">
                        {noAnswers.map((item) => (
                            <List.Item key={`${String(item.column)}-${String(item.sample)}`}>
                                <Text span inherit className="wrap-key">
                                    {nameOf(columns[item.column])}, sample {item.sample}: {item.error ?? "the agent returned nothing"}
                                </Text>
                            </List.Item>
                        ))}
                    </List>
                </Stack>
            )}
            <Group gap="md">
                {columns.map((column, index) =>
                    scenario.cells[index] == null ? null : (
                        <Anchor key={column.run.id} size="sm" href={scenarioInRunHref(column.run.id, scenario.key)}>
                            Open in run {shortId(column.run.id)}
                        </Anchor>
                    ),
                )}
            </Group>
        </Stack>
    );
}

const nameOf = (column: VariantRun | undefined) => (column === undefined ? "" : columnName(column));
