export function ChartLegend({ series }: { series: readonly { name: string; color: string }[] }) {
    return (
        <ul aria-label="Series" className="flex flex-wrap items-center gap-4">
            {series.map(({ name, color }) => (
                <li key={name} className="flex items-center gap-1.5 text-sm">
                    <span aria-hidden="true" className="size-2.5 rounded-full" style={{ backgroundColor: color }} />
                    {name}
                </li>
            ))}
        </ul>
    );
}
