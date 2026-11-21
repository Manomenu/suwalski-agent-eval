import { MistakeList } from "@maniumek/suwfrontkit";

import { headlineOf, mistakesOf } from "./client";

export function Failure({ error, className }: { error: unknown; className?: string }) {
    return (
        <div className={className}>
            <p>{headlineOf(error)}</p>
            <MistakeList mistakes={mistakesOf(error)} />
        </div>
    );
}
