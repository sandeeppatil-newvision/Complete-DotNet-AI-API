import { useEffect, useRef, useState } from "react";
import {
  uploadDocument,
  listDocuments,
  deleteDocument,
  askDocument,
} from "./api/docapi";
import "./Documents.css";

export default function DocumentsPage() {
  const [docs, setDocs] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [chats, setChats] = useState({}); // messages per document
  const [input, setInput] = useState("");
  const [uploading, setUploading] = useState(false);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState("");
  const abortRef = useRef(null);
  const fileRef = useRef(null);
  const endRef = useRef(null);

  const selected = docs.find((d) => d.id === selectedId);
  const messages = chats[selectedId] ?? [];

  useEffect(() => {
    listDocuments()
      .then(setDocs)
      .catch(() => setError("Can't reach the .NET API — is it running?"));
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  async function onFileChosen(e) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    setError("");
    setUploading(true);
    try {
      const info = await uploadDocument(file);
      setDocs((d) => [info, ...d]);
      setSelectedId(info.id);
    } catch (err) {
      setError(err.message);
    } finally {
      setUploading(false);
    }
  }

  async function onDelete(id) {
    await deleteDocument(id);
    setDocs((d) => d.filter((x) => x.id !== id));
    if (selectedId === id) setSelectedId(null);
  }

  // Update the last (assistant) message of one document's chat
  function updateLast(docId, change) {
    setChats((c) => {
      const list = [...(c[docId] ?? [])];
      list[list.length - 1] = change(list[list.length - 1]);
      return { ...c, [docId]: list };
    });
  }

  async function send() {
    const question = input.trim();
    if (!question || !selectedId || isStreaming) return;
    const docId = selectedId;

    setChats((c) => ({
      ...c,
      [docId]: [
        ...(c[docId] ?? []),
        { role: "user", text: question },
        { role: "assistant", text: "", sources: [] },
      ],
    }));
    setInput("");
    setIsStreaming(true);
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      await askDocument(docId, question, {
        signal: controller.signal,
        onSources: (items) =>
          updateLast(docId, (m) => ({ ...m, sources: items })),
        onToken: (t) => updateLast(docId, (m) => ({ ...m, text: m.text + t })),
      });
    } catch (err) {
      if (err.name !== "AbortError")
        updateLast(docId, (m) => ({
          ...m,
          text: m.text + "\n⚠️ " + err.message,
        }));
    } finally {
      setIsStreaming(false);
    }
  }

  return (
    <div className="docs">
      <aside className="docs-side">
        <h2>Documents</h2>
        <button
          className="upload"
          onClick={() => fileRef.current?.click()}
          disabled={uploading}
        >
          {uploading ? "Reading & embedding…" : "⬆ Upload PDF"}
        </button>
        <input
          ref={fileRef}
          type="file"
          accept="application/pdf"
          hidden
          onChange={onFileChosen}
        />
        {error && <p className="error">{error}</p>}

        <ul>
          {docs.map((d) => (
            <li
              key={d.id}
              className={d.id === selectedId ? "active" : ""}
              onClick={() => setSelectedId(d.id)}
            >
              <span className="name">📄 {d.fileName}</span>
              <span className="meta">
                {d.pages} pages · {d.chunks} chunks
              </span>
              <button
                className="del"
                title="Delete"
                onClick={(e) => {
                  e.stopPropagation();
                  onDelete(d.id);
                }}
              >
                ×
              </button>
            </li>
          ))}
        </ul>
      </aside>

      <main className="docs-chat">
        {!selected ? (
          <div className="empty">Upload a PDF, then ask anything about it.</div>
        ) : (
          <>
            <header>
              Asking: <b>{selected.fileName}</b>
            </header>
            <div className="msgs">
              {messages.length === 0 && (
                <div className="empty">Ask a question about this document.</div>
              )}
              {messages.map((m, i) => (
                <div key={i} className={`msg ${m.role}`}>
                  <div className="bubble">
                    {m.text || (isStreaming ? "…" : "")}
                  </div>
                  {m.role === "assistant" && m.sources?.length > 0 && (
                    <div className="sources">
                      {m.sources.map((s, j) => (
                        <span key={j} className="chip" title={s.snippet}>
                          p. {s.page}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
              ))}
              <div ref={endRef} />
            </div>
            <div className="ask">
              <input
                value={input}
                placeholder="Ask about this PDF…"
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && send()}
                disabled={isStreaming}
              />
              {isStreaming ? (
                <button onClick={() => abortRef.current?.abort()}>Stop</button>
              ) : (
                <button onClick={send}>Ask</button>
              )}
            </div>
          </>
        )}
      </main>
    </div>
  );
}
