import { Paper, Table } from "@maniumek/suwfrontkit";

import { branchesText, historyText, samplesText } from "./configuration";
import { HeaderHelp } from "../../help/HeaderHelp";
import { CONFIGURATION_HELP, REGRESSION_BRANCHES_HELP, REGRESSION_HISTORY_HELP, SAMPLES_HELP } from "./view";

export function ConfigurationSection({
    endpointUrl,
    samples,
    regression,
}: {
    endpointUrl: string;
    samples: number | null | undefined;
    regression: { branches?: readonly string[] | null; history?: number | null } | null | undefined;
}) {
    const sampleCount = samplesText(samples);
    const branches = branchesText(regression?.branches);
    const history = historyText(regression?.history);
    return (
        <div className="flex flex-col gap-3">
            <h3 className="text-[22px] font-bold">
                <HeaderHelp label="Configuration" help={CONFIGURATION_HELP} />
            </h3>
            <Paper>
                <Table>
                    <tbody>
                        <tr>
                            <th>Endpoint</th>
                            <td className="wrap-key">{endpointUrl}</td>
                        </tr>
                        <tr>
                            <th>
                                <HeaderHelp label="Samples" help={SAMPLES_HELP} />
                            </th>
                            <td>
                                <span className="flex flex-wrap gap-1">
                                    <span>{sampleCount.text}</span>
                                    {sampleCount.isDefault && <span className="text-muted-foreground">(default)</span>}
                                </span>
                            </td>
                        </tr>
                        <tr>
                            <th>
                                <HeaderHelp label="Regression branches" help={REGRESSION_BRANCHES_HELP} />
                            </th>
                            <td className="wrap-key">
                                <span className={branches.isDefault ? "text-muted-foreground" : undefined}>{branches.text}</span>
                            </td>
                        </tr>
                        <tr>
                            <th>
                                <HeaderHelp label="Regression history" help={REGRESSION_HISTORY_HELP} />
                            </th>
                            <td>
                                <span className="flex flex-wrap gap-1">
                                    <span>{history.text}</span>
                                    {history.isDefault && <span className="text-muted-foreground">(default)</span>}
                                </span>
                            </td>
                        </tr>
                    </tbody>
                </Table>
            </Paper>
        </div>
    );
}
