# Handoff: DeafDirectionalHelper — UI Overhaul, Overlay System & Audio Capture Fix

## Overview
Full redesign of the DeafDirectionalHelper WPF accessibility app (settings window, all overlay styles, dialogs, first-run, tray) plus two functional fixes: audio capture that follows the game instead of a fixed device, and session-based game detection with per-game profiles.

**Start with `IMPLEMENTATION_PLAN.md`** — the phased, spec-complete build plan written for Claude Code. This README orients; the plan is the source of truth.

## About the Design Files
The `.dc.html` files in this bundle are **design references created in HTML** — interactive prototypes showing intended look and behavior, not production code. The task is to **recreate these designs in the existing WPF/.NET 8 codebase** (`wellforce-brandon/DeafDirectionalHelper`) using its established patterns (plain WPF, NAudio, no frameworks — see repo CLAUDE.md). Open them in a browser; the overlay demos animate with simulated audio, and the Tweaks values (color scale, overlay size) map to real settings in the plan.

## Fidelity
**High-fidelity.** Colors, typography, spacing, copy, and interaction specs are final and written out numerically in `IMPLEMENTATION_PLAN.md` (§1 tokens, per-phase specs). Recreate pixel-faithfully with WPF styles/templates.

## Design map (option IDs ↔ plan phases)

| Design (in Redesign.dc.html) | What it is | Plan phase |
|---|---|---|
| 2a | Settings shell — sidebar nav (chosen direction) | Phase 3 |
| 2b–2d | Alternate settings directions (not chosen; reference only) | — |
| 2e / 2f / 2g / 2h | Overlay styles: refined side bars, edge glow, radar ring, compass strip | Phase 4 |
| 4a / 4b | Ring-ping overlay: meter vs distance mapping (distance = default) | Phase 4 |
| 2i | Loudness color scales (Thermal default, Ice, Violet, Classic) | §1.4 + Phase 4 |
| 2j | Dialogs: hotkeys, exit confirm, profile editor | Phase 5 |
| 2k | First-run wizard (3 steps) | Phase 5 |
| 2l | Tray flyout | Phase 5 |
| 3a | Audio & device page — capture modes + live signal check | Phases 1 + 3 |
| 3b | Signal doctor (wrong-device mismatch, one-click fix) | Phase 1 |
| 3c | Game-detected toasts (unknown → offer profile; known → switched + undo) | Phase 2 |
| 3d | Profiles page — per-game cards + switch behavior | Phases 2 + 3 |
| 5a–5c | Keyboard navigation demo, key map, focus specimens | Phases 0 + 3 |
| 6a–6c | Overlay scaling: universal size + per-style contextual controls | Phases 3 + 4 |
| Current UI.dc.html (1a–1g) | Pixel-faithful recreation of today's app — the baseline | reference |

## Interactions & Behavior / State / Tokens
All specified in `IMPLEMENTATION_PLAN.md`: design tokens §1.1–1.3, color-scale math §1.4, audio constants §1.5, per-screen layout + copy in Phases 3–5, overlay geometry in Phase 4, keyboard map in Phase 3, settings schema v2 + migration in Phases 2/6.

## Assets
- `Icons/` — app icons copied from the repo (already exist there; no new assets needed).
- Channel labels, glyphs, chevrons are drawn (text/geometry), not images.
- `image-slot.js` + `support.js` are prototype runtime helpers for the HTML files only — do not port.

## Files in this bundle
- `IMPLEMENTATION_PLAN.md` — the phased build plan (main document)
- `Redesign.dc.html` — all redesign options (turns 2–6), live/animated
- `Current UI.dc.html` — faithful recreation of the current app
- `support.js`, `image-slot.js` — runtime for the HTML prototypes
- `Icons/` — app icons
