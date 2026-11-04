export function modelJudgedText(model?: string | null): string {
    return model == null || model === "" ? "Judged by a model" : `Judged by ${model} model`;
}

export function modelJudgedTooltip(model?: string | null): string {
    return `${modelJudgedText(model)}: not deterministic`;
}
