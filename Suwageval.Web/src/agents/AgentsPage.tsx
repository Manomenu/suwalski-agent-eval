import { Anchor, Paper, Table, formatDateTime } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";

import { ListPagination } from "../pagination/ListPagination";
import { pageOfItems } from "../pagination/pages";
import { usePageParam } from "../pagination/usePageParam";
import { agentsQuery } from "./api";
import { Page } from "./Page";
import { agentHref } from "../routes/links";

export function AgentsPage() {
    const query = useQuery(agentsQuery());
    const [requestedPage, setRequestedPage] = usePageParam("agentsPage");

    return (
        <Page query={query} notFound="">
            {(allAgents) => {
                const { items: agents, page } = pageOfItems(allAgents, requestedPage);
                return (
                    <div className="flex flex-col gap-4">
                        <h2 className="text-[26px] leading-tight font-bold">Agents</h2>
                        {allAgents.length === 0 ? (
                            <p className="text-muted-foreground">There are no agents yet. Add the first one with suwageval push.</p>
                        ) : (
                            <Paper>
                                <Table className="min-w-[500px]">
                                    <thead>
                                        <tr>
                                            <th>Name</th>
                                            <th>Address</th>
                                            <th>Created</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {agents.map((agent) => (
                                            <tr key={agent.id}>
                                                <td>
                                                    <Anchor href={agentHref(agent.name)}>{agent.name}</Anchor>
                                                </td>
                                                <td>{agent.endpointUrl}</td>
                                                <td>{formatDateTime(agent.createdAt)}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </Table>
                                <ListPagination totalCount={allAgents.length} page={page} onChange={setRequestedPage} />
                            </Paper>
                        )}
                    </div>
                );
            }}
        </Page>
    );
}
