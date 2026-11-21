import { Anchor, Paper, Tooltip } from "@maniumek/suwfrontkit";

import { AGENT_ERRORS_EXPLANATION, AGENT_ERRORS_PAGE_SIZE, errorsOfSamples, shortError } from "./agentErrors";
import { HeaderHelp } from "../../help/HeaderHelp";
import { ListPagination } from "../../pagination/ListPagination";
import { pageOfItems } from "../../pagination/pages";
import { usePageParam } from "../../pagination/usePageParam";
import { runHref } from "../../routes/links";
import { versionTitle } from "../../format/names";
import { type AgentError } from "./api";

export function AgentErrorsCard({ errors }: { errors: AgentError[] }) {
    const [requestedPage, setRequestedPage] = usePageParam("errorsPage");
    if (errors.length === 0) return null;
    const { items: listed, page } = pageOfItems(errors, requestedPage, AGENT_ERRORS_PAGE_SIZE);
    return (
        <Paper className="flex flex-col gap-3">
            <h3 className="text-[22px] font-bold">
                <HeaderHelp label="Agent errors" help={AGENT_ERRORS_EXPLANATION} />
            </h3>
            {listed.map((error) => (
                <div key={JSON.stringify([error.agentVersionId, error.scenarioKey])} className="flex flex-col gap-0.5">
                    <div className="flex items-baseline justify-between gap-4">
                        <Anchor href={runHref(error.newestErrorRunId)} className="wrap-key">
                            {error.scenarioKey}
                        </Anchor>
                        <div className="flex items-baseline gap-2.5">
                            <span className="font-semibold">{errorsOfSamples(error)}</span>
                            <span className="text-sm text-muted-foreground">{versionTitle(error.agentVersionId, error.versionLabel)}</span>
                        </div>
                    </div>
                    <Tooltip label={error.newestError} disabled={error.newestError === ""}>
                        <p className="text-sm text-muted-foreground">{shortError(error.newestError)}</p>
                    </Tooltip>
                </div>
            ))}
            <ListPagination totalCount={errors.length} page={page} pageSize={AGENT_ERRORS_PAGE_SIZE} onChange={setRequestedPage} />
        </Paper>
    );
}
