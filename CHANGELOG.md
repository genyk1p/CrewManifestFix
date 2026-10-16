# Changelog

## 0.2.0 — 2026-10-16
- First public release. Crew Manifest no longer adds another toolbar button (and another running copy of itself) on every flight scene load (linuxgurugamer/CrewManifest#1).
- Close button "×" in the top right corner of the Crew Manifest window. It works like the toolbar button (the toolbar icon follows), and the next toolbar click opens the window again.
- The "Crewmanifest, vessel: ..." line that Crew Manifest wrote to `KSP.log` on every GUI event while the window was open is no longer written (about 15% of all log lines in a session).

## 0.1.0 — not released
- First version: only the first Crew Manifest flight behaviour is kept; later duplicates are destroyed before they add a toolbar button.
- Replaces an earlier local workaround that flipped the `once` flag of the `KSPAddon` attribute directly in `CrewManifest.dll`.
