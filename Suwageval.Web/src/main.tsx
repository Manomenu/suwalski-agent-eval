import "@mantine/core/styles.css";

import { MantineProvider, createTheme, type MantineColorsTuple } from "@mantine/core";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { App } from "./App";

// The app's colour: the burgundy of its icon (public/favicon.svg). Shade 7 is the icon's background and the primary colour on a
// light page; shade 5 is the icon's liquid, lighter, so the primary colour stays readable on a dark page.
const burgundy: MantineColorsTuple = [
    "#fcf0f2",
    "#f5dde2",
    "#e9b8c2",
    "#dc909f",
    "#cf6b7e",
    "#c2495a",
    "#9e3043",
    "#7a1f2b",
    "#621722",
    "#4a1019",
];

const theme = createTheme({
    colors: { burgundy },
    primaryColor: "burgundy",
    primaryShade: { light: 7, dark: 5 },
    defaultRadius: "md",
    fontFamily: "Inter, system-ui, -apple-system, 'Segoe UI', sans-serif",
});

const root = document.getElementById("root");
if (!root) throw new Error("index.html has no #root element");

createRoot(root).render(
    <StrictMode>
        <MantineProvider theme={theme} defaultColorScheme="auto">
            <App />
        </MantineProvider>
    </StrictMode>,
);
