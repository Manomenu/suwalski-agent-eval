import { Text } from "@mantine/core";

export function VerdictMessage({ message, byModel }: { message: string; byModel: boolean }) {
    return byModel ? (
        <Text span fs="italic" inherit>
            {message}
        </Text>
    ) : (
        message
    );
}
