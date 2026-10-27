const API = import.meta.env.VITE_API_URL;

// Same stream reader as docApi.js. Only the event types are new.
export async function runAgent(
  messages,
  { onToolCall, onToolResult, onToken, signal },
) {
  const res = await fetch(`${API}/api/AgentV1/stream`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ messages }),
    signal,
  });
  if (!res.ok || !res.body) throw new Error(`Request failed: ${res.status}`);

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });

    const events = buffer.split("\n\n");
    buffer = events.pop(); // keep any half-received event

    for (const evt of events) {
      const line = evt.trim();
      if (!line.startsWith("data:")) continue;
      const data = line.slice(5).trim();
      if (data === "[DONE]") return;

      const msg = JSON.parse(data);
      if (msg.type === "tool_call") onToolCall?.(msg);
      else if (msg.type === "tool_result") onToolResult?.(msg);
      else if (msg.type === "token") onToken?.(msg.text);
    }
  }
}
