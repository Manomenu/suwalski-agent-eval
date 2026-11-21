import { ApiError } from "@maniumek/suwfrontkit";
import { describe, expect, it } from "vitest";

import { headlineOf, isNotFound, messageOf, mistakesOf } from "./client";

const refusal = new ApiError(422, {
    title: "The request is not valid",
    detail: "2 mistakes in `mistakes`: fix each as its `fix` says, then try again.",
    code: "request.invalid",
    mistakes: [
        { code: "field.invalid", message: "The note is at most 500 characters.", fix: "Shorten the note.", at: "note" },
        { code: "field.invalid", message: "The page is below 1.", fix: "Ask for page 1 or later.", at: "page" },
    ],
});

describe("isNotFound", () => {
    it("is true only for a 404 from the server", () => {
        expect(isNotFound(new ApiError(404, { detail: "x" }))).toBe(true);
        expect(isNotFound(new ApiError(500, { detail: "x" }))).toBe(false);
        expect(isNotFound(new Error("x"))).toBe(false);
    });
});

describe("a refusal with mistakes", () => {
    it("is headed by its title, not by the detail that points at the mistakes", () => {
        expect(headlineOf(refusal)).toBe("The request is not valid");
    });

    it("reads its mistakes with their codes, typed by the contract", () => {
        expect(mistakesOf(refusal).map((mistake) => [mistake.code, mistake.at])).toEqual([
            ["field.invalid", "note"],
            ["field.invalid", "page"],
        ]);
    });

    it("says each mistake and its fix in one line where only a line fits", () => {
        expect(messageOf(refusal)).toBe(
            "The note is at most 500 characters. Shorten the note. The page is below 1. Ask for page 1 or later.",
        );
    });
});

describe("an answer without mistakes", () => {
    it("says the detail of a refusal, the text of any other error, and never throws", () => {
        const stale = new ApiError(409, { title: "Run group not archived", detail: "Archive it first.", code: "runGroup.notArchived" });
        expect([headlineOf(stale), messageOf(stale)]).toEqual(["Archive it first.", "Archive it first."]);
        expect(messageOf(new ApiError(503, "<html>busy</html>", "Service Unavailable"))).toBe("503 Service Unavailable");
        expect(messageOf(new Error("offline"))).toBe("offline");
        expect(messageOf("gone")).toBe("gone");
        expect(mistakesOf(new Error("offline"))).toEqual([]);
    });
});
