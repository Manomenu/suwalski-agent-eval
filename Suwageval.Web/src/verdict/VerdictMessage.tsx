export function VerdictMessage({ message, byModel }: { message: string; byModel: boolean }) {
    return byModel ? <span className="italic">{message}</span> : message;
}
