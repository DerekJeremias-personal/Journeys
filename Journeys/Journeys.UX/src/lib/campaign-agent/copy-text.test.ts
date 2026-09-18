import { describe, expect, it, vi } from "vitest";
import { copyText } from "./copy-text";

describe("copyText", () => {
  it("writes the string to the clipboard and returns true", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    await expect(copyText("conv-1", { writeText })).resolves.toBe(true);
    expect(writeText).toHaveBeenCalledWith("conv-1");
  });

  it("returns false when clipboard is missing or write fails", async () => {
    await expect(copyText("conv-1", undefined)).resolves.toBe(false);
    await expect(copyText("", { writeText: vi.fn() })).resolves.toBe(false);
    const writeText = vi.fn().mockRejectedValue(new Error("denied"));
    await expect(copyText("conv-1", { writeText })).resolves.toBe(false);
  });
});
