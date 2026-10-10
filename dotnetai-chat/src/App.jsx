import { useEffect, useRef, useState } from "react";
import { streamChat, clearChat } from "./api/chatStream";
import "./App.css";

function newSessionId() {
  const id = crypto.randomUUID();
  localStorage.setItem("sessionId", id);
  return id;
}

export default function App() {
  // Same session ID across refreshes = the API keeps remembering
  const [sessionId, setSessionId] = useState(
    () => localStorage.getItem("sessionId") ?? newSessionId(),
  );
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState(false);
  const [error, setError] = useState("");
  const abortRef = useRef(null);
  const endRef = useRef(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  async function send(e) {
    e.preventDefault();
    const text = input.trim();
    if (!text || streaming) return;

    setInput("");
    setError("");
    // Add the user message and an empty assistant message to fill in
    setMessages((m) => [
      ...m,
      { role: "user", text },
      { role: "assistant", text: "" },
    ]);
    setStreaming(true);
    abortRef.current = new AbortController();

    try {
      await streamChat(
        sessionId,
        text,
        (token) =>
          setMessages((m) => {
            const copy = [...m];
            const last = copy[copy.length - 1];
            copy[copy.length - 1] = { ...last, text: last.text + token };
            return copy;
          }),
        abortRef.current.signal,
      );
    } catch (err) {
      if (err.name !== "AbortError") setError(err.message);
    } finally {
      setStreaming(false);
    }
  }

  async function newChat() {
    abortRef.current?.abort();
    try {
      await clearChat(sessionId);
    } catch {
      // API offline — still start fresh locally
    }
    setSessionId(newSessionId());
    setMessages([]);
    setError("");
  }

  return (
    <div className="app">
      <header>
        <div>
          <h1>DotNetAI Chat</h1>
          <small>
            React + .NET + Azure OpenAI · session {sessionId.slice(0, 8)}
          </small>
        </div>
        <button className="secondary" onClick={newChat}>
          New chat
        </button>
      </header>

      <main className="messages">
        {messages.length === 0 && (
          <p className="empty">
            Ask anything. The AI remembers this conversation.
          </p>
        )}
        {messages.map((m, i) => (
          <div key={i} className={`msg ${m.role}`}>
            {m.text}
            {streaming &&
              i === messages.length - 1 &&
              m.role === "assistant" && <span className="cursor" />}
          </div>
        ))}
        <div ref={endRef} />
      </main>

      {error && <p className="error">{error}</p>}

      <form onSubmit={send}>
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Message DotNetAI..."
          disabled={streaming}
        />
        {streaming ? (
          <button type="button" onClick={() => abortRef.current?.abort()}>
            Stop
          </button>
        ) : (
          <button type="submit" disabled={!input.trim()}>
            Send
          </button>
        )}
      </form>
    </div>
  );
}
