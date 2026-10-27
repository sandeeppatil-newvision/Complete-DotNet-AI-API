import { useEffect, useRef, useState } from "react";
import { runAgent } from "./api/agentStream";
import "./Agent.css";

const SUGGESTIONS = [
  "How many documents have I uploaded?",
  "I travel for 5 nights. What's the most I can spend on hotel plus 5 days of meals?",
  "My learning budget is used for a ₹18,500 course. How much is left?",
  "What is the CEO's salary?",
];

function prettyArgs(json) {
  try {
    return Object.entries(JSON.parse(json))
      .map(([k, v]) => `${k}: ${v}`)
      .join(" · ");
  } catch {
    return json;
  }
}

export default function AgentPage() {
  const [messages, setMessages] = useState([]); // { role, content, steps? }
  const [input, setInput] = useState("");
  const [isStreaming, setIsStreaming] = useState(false);
  const abortRef = useRef(null);
  const endRef = useRef(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // change only the last (assistant) message
  const updateLast = (fn) =>
    setMessages((m) => m.map((x, i) => (i === m.length - 1 ? fn(x) : x)));

  async function send(text) {
    const question = (text ?? input).trim();
    if (!question || isStreaming) return;

    setInput("");
    const history = [...messages, { role: "user", content: question }];
    setMessages([...history, { role: "assistant", content: "", steps: [] }]);
    setIsStreaming(true);

    const controller = new AbortController();
    abortRef.current = controller;

    try {
      await runAgent(
        history.map(({ role, content }) => ({ role, content })), // send text only, not the trace
        {
          signal: controller.signal,
          onToolCall: (c) =>
            updateLast((a) => ({
              ...a,
              steps: [
                ...a.steps,
                { id: c.id, name: c.name, args: c.args, result: null },
              ],
            })),
          onToolResult: (r) =>
            updateLast((a) => ({
              ...a,
              steps: a.steps.map((s) =>
                s.id === r.id ? { ...s, result: r.text } : s,
              ),
            })),
          onToken: (t) => updateLast((a) => ({ ...a, content: a.content + t })),
        },
      );
    } catch (err) {
      if (err.name !== "AbortError")
        updateLast((a) => ({
          ...a,
          content: a.content || `⚠️ ${err.message}`,
        }));
    } finally {
      setIsStreaming(false);
      abortRef.current = null;
    }
  }

  return (
    <div className="agent">
      <div className="agent-msgs">
        {messages.length === 0 && (
          <div className="agent-empty">
            <h2>🤖 AI Agent</h2>
            <p>
              It decides which tool to use: search your PDFs, calculate, or just
              answer.
            </p>
            <div className="chips">
              {SUGGESTIONS.map((s) => (
                <button key={s} onClick={() => send(s)}>
                  {s}
                </button>
              ))}
            </div>
          </div>
        )}

        {messages.map((m, i) => (
          <div key={i} className={`bubble ${m.role}`}>
            {m.steps?.map((s) => (
              <div
                key={s.id}
                className={`step ${s.result === null ? "running" : "done"}`}
              >
                <div className="step-head">
                  <span className="dot" /> 🔧 <b>{s.name}</b>
                  <span className="args">{prettyArgs(s.args)}</span>
                </div>
                {s.result !== null && (
                  <div className="step-result">↳ {s.result}</div>
                )}
              </div>
            ))}
            {m.content}
            {isStreaming &&
              i === messages.length - 1 &&
              m.role === "assistant" &&
              !m.content &&
              !m.steps?.length &&
              "…"}
          </div>
        ))}
        <div ref={endRef} />
      </div>

      <form
        className="agent-input"
        onSubmit={(e) => {
          e.preventDefault();
          send();
        }}
      >
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Ask about your documents, or ask for a calculation…"
          disabled={isStreaming}
        />
        {isStreaming ? (
          <button
            type="button"
            className="stop"
            onClick={() => abortRef.current?.abort()}
          >
            ■ Stop
          </button>
        ) : (
          <button type="submit">Send</button>
        )}
      </form>
    </div>
  );
}
