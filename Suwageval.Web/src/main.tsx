import "./index.css";
import "./app.css";
import "./print/print.css";

import { ErrorBoundary } from "@maniumek/suwfrontkit";
import { createQueryClient } from "@maniumek/suwfrontkit/query";
import { QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { App } from "./App";
import { PageFailed } from "./shell/PageFailed";

const root = document.getElementById("root");
if (!root) throw new Error("index.html has no #root element");

createRoot(root, {
    onUncaughtError: (error, info) => {
        console.error("Uncaught render error", error, info.componentStack);
    },
    onCaughtError: (error, info) => {
        console.error("Render error caught by an error boundary", error, info.componentStack);
    },
}).render(
    <StrictMode>
        <QueryClientProvider client={createQueryClient()}>
            <ErrorBoundary fallback={PageFailed}>
                <App />
            </ErrorBoundary>
        </QueryClientProvider>
    </StrictMode>,
);
