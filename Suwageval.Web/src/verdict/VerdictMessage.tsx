import { Text } from "@mantine/core";

/** Why a check passed or failed; a model's own words (a judged verdict) in italics, a rule's message as it is. */
export function VerdictMessage({ message, byModel }: { message: string; byModel: boolean }) {
    return byModel ? (
        <Text span fs="italic" inherit>
            {message}
        </Text>
    ) : (
        message
    );
}
