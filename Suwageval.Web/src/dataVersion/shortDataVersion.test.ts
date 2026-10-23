import { describe, expect, it } from "vitest";

import { shortDataVersion } from "./shortDataVersion";

describe("shortDataVersion", () => {
    it("keeps a short data version and cuts a long one to its start", () => {
        expect(shortDataVersion("build-7")).toBe("build-7");
        expect(shortDataVersion("93a4af9723bf03c6f0d0d6825d1e7786b5015441813b5273fe381e0d2f7351eb")).toBe("93a4af97…");
    });
});
