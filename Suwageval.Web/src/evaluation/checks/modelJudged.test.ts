import { describe, expect, it } from "vitest";

import { modelJudgedText, modelJudgedTooltip } from "./modelJudged";

describe("modelJudgedTooltip", () => {
    it("names the model when it is known", () => {
        expect(modelJudgedTooltip("test-judge-model")).toBe("Judged by test-judge-model model: not deterministic");
        expect(modelJudgedText("test-judge-model")).toBe("Judged by test-judge-model model");
    });

    it("falls back to the generic text when the model is unknown", () => {
        expect(modelJudgedTooltip(null)).toBe("Judged by a model: not deterministic");
        expect(modelJudgedTooltip(undefined)).toBe("Judged by a model: not deterministic");
        expect(modelJudgedTooltip("")).toBe("Judged by a model: not deterministic");
        expect(modelJudgedText(null)).toBe("Judged by a model");
    });
});
