import { Text } from "@mantine/core";

import type { RunSummary } from "./api";
import { checksCounts, samplesCounts } from "./results";

function Counts({ summary, problem }: { summary: string; problem: string | null }) {
    return (
        <Text span inherit>
            {summary}
            {problem !== null && (
                <Text span inherit c="red">
                    {" · "}
                    {problem}
                </Text>
            )}
        </Text>
    );
}

export function ChecksCount({ run }: { run: Pick<RunSummary, "checksPassed" | "checksFailed"> }) {
    return <Counts {...checksCounts(run)} />;
}

export function SamplesCount({ run }: { run: Pick<RunSummary, "samplesTotal" | "samplesDone" | "samplesFailed"> }) {
    return <Counts {...samplesCounts(run)} />;
}
