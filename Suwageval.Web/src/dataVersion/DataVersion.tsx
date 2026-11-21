import { CopyButton, Tooltip } from "@maniumek/suwfrontkit";

import { shortDataVersion } from "./shortDataVersion";

const TEXT_SIZE = { xs: "text-xs", sm: "text-sm" };

export function DataVersion({
    dataVersion,
    description,
    size = "xs",
}: {
    dataVersion: string;
    description?: string | null | undefined;
    size?: "xs" | "sm";
}) {
    const text = description?.trim();
    return (
        <div className="flex flex-col">
            <div className="flex items-center gap-1">
                <p className={TEXT_SIZE[size]}>
                    Data{" "}
                    <Tooltip label={dataVersion}>
                        <span className="cursor-help">{shortDataVersion(dataVersion)}</span>
                    </Tooltip>
                </p>
                <CopyButton
                    className="no-print"
                    variant="ghost"
                    size="xs"
                    value={dataVersion}
                    label="Copy the data version"
                    copiedLabel="Copied"
                />
            </div>
            {text ? (
                <Tooltip label={text}>
                    <p className={`${TEXT_SIZE[size]} line-clamp-2 w-fit max-w-full text-muted-foreground`}>{text}</p>
                </Tooltip>
            ) : null}
        </div>
    );
}
