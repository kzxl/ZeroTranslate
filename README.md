# 🌌 ZeroTranslate — Instant On-Screen Translation Suite

[![Type: Desktop Application](https://img.shields.io/badge/Type-Desktop%20Application-007ACC?style=flat-square&logo=windows)](https://github.com/kzxl/ZeroTranslate)
[![Ecosystem](https://img.shields.io/badge/Ecosystem-ZeroUniverse-8A2BE2?style=flat-square)](https://github.com/kzxl/ZeroUniverse)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?style=flat-square&logo=windows)](https://dotnet.microsoft.com/)
[![Distribution: Standalone Single-File](https://img.shields.io/badge/Distribution-Standalone%20Single--File-2ea44f?style=flat-square)](https://github.com/kzxl/ZeroTranslate)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)


**ZeroTranslate** is a lightweight, blazing-fast desktop translation tool for Windows. Designed with C# WPF and .NET 8, it provides an exceptionally smooth, distraction-free translation experience directly at your cursor. Part of the sovereign **ZeroUniverse** application suite, it guarantees low resource overhead and instantaneous response times.

---

## 🚀 Key Features

*   **⚡ Instantly Translate Anywhere**: Highlight text in any application and press your configured hotkey to immediately view translations without switching browser tabs.
*   **🖱️ Smart Floating Popup**: Press `Ctrl + Q` to open a sleek, transparent translation popup near your cursor. It automatically dismisses when clicking outside.
*   **🖥️ Detailed Main Window**: Press `Ctrl + Enter` to open a full window for editing lengthy translations, checking character counts, and browsing comprehensive dictionary outputs.
*   **🧠 Intelligent Auto-Language Flipper**: Advanced context detection automatically reverses source and target languages when matching native text (e.g., Vietnamese ➔ English automatically).
*   **🔀 Quick Language Swapping**: 1-click reversal of Source and Target languages in both the Main and Popup windows.
*   **🌙 Dark Mode First**: Features a modern Dark UI with glassmorphism effects, ensuring zero eye strain during late-night programming or research sessions.
*   **⚙️ Multi-Engine Extensibility**: Modular provider architecture currently supporting *MyMemory*, with extensible adapters for Google, Bing, and DeepL engines.

---

## ⌨️ Default Hotkeys

- `Ctrl + Q`: Activate Floating Popup Translation.
- `Ctrl + Enter`: Open Detailed Main Translation Window.
- `Esc`: Close open translation windows immediately.

---

## 📦 Deployment & Binaries

ZeroTranslate is distributed in two deployment formats. Run `build.ps1` to compile:

- **Lightweight Build (~5MB)**: Ultra-fast executable. Requires the `.NET 8 Desktop Runtime`.
- **Standalone Build (~68MB)**: Fully self-contained single file executable. Runs out-of-the-box on any 64-bit Windows machine without external dependencies.

Outputs are generated in `PublishOutput/Lightweight/` and `PublishOutput/Standalone/`.

---

## 🛠️ Internal Architecture

Built with strict MVVM separation in C# WPF:
*   `Windows API`: Global Keyboard Hooks (`SetWindowsHookEx`) to listen for hotkeys globally without stealing focus.
*   `SendKeys API`: Automates system clipboard interaction (`^c`) for blazing-fast 10ms text capturing from foreign windows.
*   `Dependency Injection`: Autofac container decoupling translation providers, hotkey listeners, and UI views.

---

## 📄 License

Licensed under the **MIT License**. Part of the sovereign **ZeroUniverse** industrial computing ecosystem.
