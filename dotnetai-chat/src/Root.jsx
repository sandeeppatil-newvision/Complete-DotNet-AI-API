import { useState } from "react";
import App from "./App";
import DocumentsPage from "./DocumentsPage";
import AgentPage from "./AgentPage";
import "./Root.css";

export default function Root() {
  const [tab, setTab] = useState("chat");

  return (
    <>
      <nav className="tabs">
        <button
          className={tab === "chat" ? "on" : ""}
          onClick={() => setTab("chat")}
        >
          💬 Chat
        </button>
        <button
          className={tab === "docs" ? "on" : ""}
          onClick={() => setTab("docs")}
        >
          📄 Documents
        </button>

        <button
          className={tab === "agent" ? "on" : ""}
          onClick={() => setTab("agent")}
        >
          🤖 Agent
        </button>
      </nav>
      {/* hidden instead of unmounted, so each tab keeps its state */}
      <div hidden={tab !== "chat"}>
        <App />
      </div>
      <div hidden={tab !== "docs"}>
        <DocumentsPage />
      </div>
      <div hidden={tab !== "agent"}>
        <AgentPage />
      </div>
    </>
  );
}
