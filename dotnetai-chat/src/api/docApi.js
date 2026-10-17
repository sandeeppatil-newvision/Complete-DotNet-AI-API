const API = import.meta.env.VITE_API_URL;

export async function uploadDocument(file) {
  const form = new FormData();
  form.append("file", file); // must match the IFormFile parameter name
  const res = await fetch(`${API}/api/Documents`, {
    method: "POST",
    body: form,
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export async function listDocuments() {
  const res = await fetch(`${API}/api/Documents`);
  if (!res.ok) throw new Error(`Request failed: ${res.status}`);
  return res.json();
}

export async function deleteDocument(id) {
  await fetch(`${API}/api/Documents/${id}`, { method: "DELETE" });
}

// Same stream reader as chatStream.js — but each event is now { type, ... }
export async function askDocument(
  id,
  question,
  { onSources, onToken, signal },
) {
  const res = await fetch(`${API}/api/Documents/${id}/ask/stream`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question }),
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
      if (msg.type === "sources") onSources?.(msg.items);
      else if (msg.type === "token") onToken?.(msg.text);
    }
  }
}
