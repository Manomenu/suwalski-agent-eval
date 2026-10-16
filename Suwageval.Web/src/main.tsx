import "@mantine/core/styles.css";
import "@mantine/charts/styles.css";
import "./app.css";

import { MantineProvider, createTheme, type MantineColorsTuple } from "@mantine/core";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { App } from "./App";

// Catppuccin Mocha (https://catppuccin.com/palette), the dark flavour, and only that one: the app is always dark.
// `dark` carries Mocha's surfaces and text, lightest first (Mantine's order): text, subtext 1 and 0, overlay 1, surface 2, 1, 0,
// base (the page), mantle, crust. Each accent is a tuple around Mocha's colour at shade 5 (lighter towards white, darker towards
// crust), so Mantine's named colours — red failed, green passed, yellow not judged — are Mocha's.
const dark: MantineColorsTuple = [
    "#cdd6f4",
    "#bac2de",
    "#a6adc8",
    "#7f849c",
    "#585b70",
    "#45475a",
    "#313244",
    "#1e1e2e",
    "#181825",
    "#11111b",
];
const gray: MantineColorsTuple = [
    "#cdd6f4",
    "#bac2de",
    "#a6adc8",
    "#9399b2",
    "#7f849c",
    "#6c7086",
    "#585b70",
    "#45475a",
    "#313244",
    "#1e1e2e",
];
const mauve: MantineColorsTuple = [
    "#f7f2fe",
    "#efe4fd",
    "#e8d7fb",
    "#e0cafa",
    "#d5b8f9",
    "#cba6f7",
    "#a688cb",
    "#816a9f",
    "#65547e",
    "#493e5d",
];
const red: MantineColorsTuple = [
    "#fdeef2",
    "#fbdce5",
    "#facbd8",
    "#f8b9cb",
    "#f5a2b9",
    "#f38ba8",
    "#c6738c",
    "#995a70",
    "#77485a",
    "#553645",
];
const green: MantineColorsTuple = [
    "#f2fbf1",
    "#e4f7e3",
    "#d7f2d5",
    "#caeec7",
    "#b8e9b4",
    "#a6e3a1",
    "#88b986",
    "#6a8f6b",
    "#546f57",
    "#3e5043",
];
const yellow: MantineColorsTuple = [
    "#fefbf3",
    "#fdf6e7",
    "#fcf2db",
    "#fbeecf",
    "#fae8bf",
    "#f9e2af",
    "#cbb891",
    "#9c8e74",
    "#796f5e",
    "#575047",
];
const blue: MantineColorsTuple = [
    "#edf4fe",
    "#dce8fe",
    "#caddfd",
    "#b8d2fc",
    "#a1c3fb",
    "#89b4fa",
    "#7193cd",
    "#5973a1",
    "#475a7f",
    "#35425e",
];
const orange: MantineColorsTuple = [
    "#fef4ed",
    "#fee8db",
    "#fdddc9",
    "#fcd1b7",
    "#fbc29f",
    "#fab387",
    "#cb9371",
    "#9d725c",
    "#7a5a4c",
    "#57423b",
];

const teal: MantineColorsTuple = [
    "#f4fcfb",
    "#e7f9f6",
    "#dbf5f1",
    "#caf0ea",
    "#b2eae1",
    "#94e2d5",
    "#7ab8b0",
    "#608e8b",
    "#4c6f6f",
    "#385053",
];
const pink: MantineColorsTuple = [
    "#fef9fd",
    "#fdf2fa",
    "#fceaf7",
    "#fae0f3",
    "#f8d3ee",
    "#f5c2e7",
    "#c79fbe",
    "#9a7b95",
    "#786177",
    "#554658",
];

const theme = createTheme({
    colors: { dark, gray, mauve, red, green, yellow, blue, orange, teal, pink },
    primaryColor: "mauve",
    primaryShade: 5,
    // Mocha's accents are pastel: text on a filled one is dark, picked by Mantine.
    autoContrast: true,
    black: "#11111b",
    defaultRadius: "md",
    fontFamily: "Inter, system-ui, -apple-system, 'Segoe UI', sans-serif",
});

const root = document.getElementById("root");
if (!root) throw new Error("index.html has no #root element");

createRoot(root).render(
    <StrictMode>
        <MantineProvider theme={theme} forceColorScheme="dark">
            <App />
        </MantineProvider>
    </StrictMode>,
);
