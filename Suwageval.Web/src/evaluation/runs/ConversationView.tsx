import { ConversationItems } from "../../conversation/ConversationItems";
import type { ConversationItem } from "./api";
import { simulationTurns } from "./simulation";

export function ConversationView({
    items,
    highlight,
    simulated = false,
}: {
    items: ConversationItem[];
    highlight: Set<string>;
    simulated?: boolean;
}) {
    if (items.length === 0) return <p className="text-muted-foreground">No output.</p>;
    if (!simulated) return <ConversationItems items={items} highlight={highlight} />;
    return (
        <div className="flex flex-col gap-4">
            {simulationTurns(items).map((turn, index) => (
                <div key={index} className="flex flex-col gap-2.5">
                    <h5 className="text-base font-bold">Turn {index + 1}</h5>
                    <ConversationItems items={turn} highlight={highlight} simulatedUser />
                </div>
            ))}
        </div>
    );
}
