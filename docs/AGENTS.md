# Documentation Rules

When editing or creating files in the `docs/` folder, AI agents must follow these rules:

1. **Simple English**: Use clear, direct, and simple English. Avoid complex idioms, metaphors, or advanced vocabulary. Many users speak English as a second language, and this text is often put through machine translation. Keep sentences short.

2. **Image Placeholders**: When documenting a UI feature, include a placeholder for a screenshot if one does not exist. Use standard markdown image syntax pointing to the `images/` folder (or `../images/` if inside a subfolder). The alt-text must begin with `PLACEHOLDER:` and clearly describe what the user needs to screenshot. Example: `![PLACEHOLDER: Screenshot of Autopot tab showing HP at 50%](../images/autopot-example.png)`
   - **Naming convention**: name screenshot files `{doc-name}-{short-description}.png` (e.g. `getting-started-profile.png`, `autopot-hp-threshold.png`) so images stay grouped by the doc they belong to and never collide.

3. **No Code in General Docs**: Avoid discussing C#, architecture, or IPC protocols unless you are writing a specific developer section. Focus on the end-user experience and how to use the tool.
   - If developer-facing detail is genuinely needed, put it in `docs/dev/` in its own file — never mixed into a user-facing tab doc. Link to it rather than inlining it.

4. **Tone**: Helpful, direct, and concise. Do not add fluff.

5. **App Name**: Although the internal code and repository use "ORTools", in all user-facing documentation in this folder you **must** refer to the app as "OSRO Tools".

6. **Document Structure**: Tab and feature docs should follow this skeleton:
   - A one- or two-sentence summary of what the feature does and why a user would use it.
   - Numbered setup/configuration steps (see rule 8 for when to number vs. bullet).
   - A short "Tips" or "Common Issues" section if relevant.
   - One placeholder screenshot near the end, per rule 2.

7. **UI Element Formatting**: Bold any exact button, tab name, menu item, or text the user needs to click, type, or read on screen (e.g. **Settings**, **Save**, **Run as Administrator**). Do not bold general nouns that aren't literal UI text.

8. **Numbered vs. Bulleted Lists**: 
   - Use `##` numbered headers for major stages of a doc (e.g. `## 1. Running the App`).
   - Use bullet points for supporting details, explanations, or "why" statements within a section.
   - Use a numbered list only for steps the user must follow in order (click-through instructions).

9. **Cross-Linking**: If a feature depends on another tab being set up first, or naturally relates to another doc, link to it the first time it's mentioned, using a relative path (e.g. `[Troubleshooting](troubleshooting.md)`, or `[Autopot](tabs/autopot.md)` from a subfolder). Don't over-link — one link per related topic per page is enough.

10. **Callouts/Warnings**: For critical warnings (e.g. required permissions, destructive actions, things that will break the tool if skipped), use a blockquote in this format so they visually stand out:
    ```
    > **Warning:** OSRO Tools must run as Administrator, or HP/SP detection will not work.
    ```
    Use `> **Note:**` for helpful-but-non-critical asides.

11. **Terminology Consistency**: Always use the same term for the same concept, across all docs. Do not vary wording for variety — consistency matters more here than usual because of machine translation (see rule 1). Standard terms to use:
    - **HP/SP** — never "life/mana" or "health/mana points"
    - **Buff** — never "status effect" or "enhancement" (unless referring to a debuff, see below)
    - **Debuff** — never "negative status" or "ailment"
    - **Macro** — never "script" or "automation routine"
    - **Spam** (as in Skill Spammer) — never "repeat" or "loop" when referring to the feature by name
    - **Profile** — never "preset" or "config"
    - **Tab** — never "panel" or "section" when referring to the side menu items
