export type ChatComposerKeyEvent = {
  key: string;
  shiftKey: boolean;
  keyCode?: number;
  nativeEvent?: { isComposing?: boolean };
};

/** Enter sends. Shift+Enter inserts a newline. IME composition does not send. */
export function shouldSubmitChatOnEnter(event: ChatComposerKeyEvent): boolean {
  if (event.nativeEvent?.isComposing || event.keyCode === 229) return false;
  return event.key === "Enter" && !event.shiftKey;
}
