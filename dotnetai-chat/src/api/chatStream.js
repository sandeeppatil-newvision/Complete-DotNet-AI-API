const API = import.meta.env.VITE_API_URL;

// POST a message and call onToken for every streamed token
export async function streamChat(sessionId, message, onToken, signal) {
  const res = await fetch(`${API}/api/Conversation/stream`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sessionId, message }),
    signal,
  });

  if (!res.ok || !res.body) throw new Error(`API error ${res.status}`);

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });

    // SSE events are separated by a blank line
    const events = buffer.split("\n\n");
    buffer = events.pop(); // keep the last, possibly incomplete, event

    for (const event of events) {
      if (!event.startsWith("data: ")) continue;
      const data = event.slice(6);
      if (data === "[DONE]") return;
      onToken(JSON.parse(data)); // server sent each token as a JSON string
    }
  }
}

// Forget this session on the server
export async function clearChat(sessionId) {
  await fetch(`${API}/api/Conversation/${sessionId}`, { method: "DELETE" });
}
