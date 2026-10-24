/** The text that says a model gives a check's verdict: names the model when it is known. */
export function modelJudgedText(model?: string | null): string {
    return model == null || model === "" ? "Judged by a model" : `Judged by ${model} model`;
}

/** The tooltip of the robot icon: the same text, and that such a verdict is not deterministic. */
export function modelJudgedTooltip(model?: string | null): string {
    return `${modelJudgedText(model)}: not deterministic`;
}
