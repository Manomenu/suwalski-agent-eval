import { describe, expect, it } from "vitest";

import { checkKeys, dayMarks, hasFailures, passRateRows, rateText, volumeRows, volumeSeries, type Trend } from "./monitoring";

const trend: Trend = {
    days: [
        { date: "2026-10-23", environment: "production", conversations: 3, checked: 3 },
        { date: "2026-10-23", environment: "staging", conversations: 1, checked: 0 },
        { date: "2026-10-24", environment: "production", conversations: 2, checked: 0 },
    ],
    points: [
        { date: "2026-10-23", checkKey: "polite", passed: 1, checked: 1 },
        { date: "2026-10-23", checkKey: "no-refund", passed: 1, checked: 2 },
        { date: "2026-10-23", checkKey: "no-refund", passed: 1, checked: 1 },
    ],
    versions: [
        { at: "2026-10-23T08:00:00Z", agentVersionId: "aaaaaaaa-1111", label: "v7" },
        { at: "2026-10-24T09:00:00Z", agentVersionId: "bbbbbbbb-2222", label: null },
    ],
    dataVersions: [{ at: "2026-10-24T10:00:00Z", dataVersion: "index-8" }],
};

describe("rateText", () => {
    it("writes the share with the counts, or a dash for no conversation", () => {
        expect(rateText({ passed: 4, checked: 5 })).toBe("80% (4/5)");
        expect(rateText({ passed: 0, checked: 0 })).toBe("—");
    });
});

describe("hasFailures", () => {
    it("is true only when a conversation failed", () => {
        expect(hasFailures({ passed: 4, checked: 5 })).toBe(true);
        expect(hasFailures({ passed: 5, checked: 5 })).toBe(false);
        expect(hasFailures({ passed: 0, checked: 0 })).toBe(false);
    });
});

describe("volumeRows", () => {
    it("has the conversations and the checked ones per day", () => {
        expect(volumeRows(trend)).toEqual([
            { date: "2026-10-23", Conversations: 4, Checked: 3, production: 3, staging: 1 },
            { date: "2026-10-24", Conversations: 2, Checked: 0, production: 2 },
        ]);
        expect(volumeSeries(trend)).toEqual(["Conversations", "production", "staging", "Checked"]);
        expect(volumeSeries({ ...trend, days: trend.days.filter((day) => day.environment === "production") })).toEqual([
            "Conversations",
            "Checked",
        ]);
    });
});

describe("passRateRows", () => {
    it("adds up the versions of a day, and leaves a day without verdicts empty", () => {
        expect(checkKeys(trend)).toEqual(["no-refund", "polite"]);
        expect(passRateRows(trend)).toEqual([
            { date: "2026-10-23", "no-refund": 66.7, polite: 100 },
            { date: "2026-10-24", "no-refund": null, polite: null },
        ]);
    });
});

describe("dayMarks", () => {
    it("marks deploys by label or short id and data changes, joined per day", () => {
        expect(dayMarks(trend)).toEqual([
            { x: "2026-10-23", label: "version v7" },
            { x: "2026-10-24", label: "version bbbbbbbb, data index-8" },
        ]);
    });
});
