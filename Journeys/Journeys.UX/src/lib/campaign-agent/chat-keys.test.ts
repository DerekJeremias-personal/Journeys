import { describe, expect, it } from "vitest";
import { shouldSubmitChatOnEnter } from "./chat-keys";

describe("shouldSubmitChatOnEnter", () => {
  it("submits on Enter without Shift", () => {
    expect(shouldSubmitChatOnEnter({ key: "Enter", shiftKey: false })).toBe(true);
  });

  it("does not submit on Shift+Enter", () => {
    expect(shouldSubmitChatOnEnter({ key: "Enter", shiftKey: true })).toBe(false);
  });

  it("does not submit on other keys", () => {
    expect(shouldSubmitChatOnEnter({ key: "a", shiftKey: false })).toBe(false);
  });

  it("does not submit while an IME composition is active", () => {
    expect(
      shouldSubmitChatOnEnter({
        key: "Enter",
        shiftKey: false,
        nativeEvent: { isComposing: true }
      })
    ).toBe(false);
    expect(shouldSubmitChatOnEnter({ key: "Enter", shiftKey: false, keyCode: 229 })).toBe(false);
  });
});
