# Contributing to LibreSpot

Thanks for your interest in contributing.

## Reporting Issues

Use the [issue templates](https://github.com/SysAdminDoc/LibreSpot/issues/new/choose):

- **Bug report** — something broke or behaves unexpectedly.
- **Compatibility report** — a Spotify or Spicetify version that does not work.
- **Feature request** — suggest an improvement or new capability.

For security vulnerabilities, use [private vulnerability reporting](https://github.com/SysAdminDoc/LibreSpot/security/advisories/new).

## Code Contributions

1. Fork the repository and create a branch from `main`.
2. Make your changes, matching the existing code style and patterns.
3. Run `dotnet test tests/LibreSpot.Desktop.Tests` and fix any failures.
4. Test PowerShell changes on both Windows PowerShell 5.1 and PowerShell 7.
5. Open a pull request using the provided template.

### Style

- Match existing naming, spacing, and structure in the file you are editing.
- WPF/C#: the project targets .NET 10 for Windows. Run `dotnet build` before submitting.
- PowerShell: both `LibreSpot.ps1` and `Backend/LibreSpot.Backend.ps1` must parse cleanly on 5.1 and 7.
- Keep commits focused on "why", not "what". One logical change per commit.

### Translations

LibreSpot ships English (`Strings.resx`) plus Russian, Simplified Chinese,
Brazilian Portuguese, and Spanish (`Strings.<culture>.resx` under
`src/LibreSpot.Desktop/Properties/`). To add or correct a translation:

1. Edit only the `Strings.<culture>.resx` file for your language. English
   (`Strings.resx`) is the source of truth — never translate by editing it.
2. Translate the `value` of each `<data>` entry. **Keep every `{0}`, `{1}`, …
   placeholder exactly as-is** (you may reorder them for grammar, e.g. `{1} {0}`,
   but you must not drop, add, or renumber them). A mismatch crashes
   `string.Format` at runtime, so the validator rejects it.
3. Run the localization gate before opening a PR:

   ```powershell
   pwsh -File tools/Sync-Localization.ps1 -Validate
   ```

   It fails on missing/stale keys, empty values, truncated sentences, and
   placeholder-count mismatches. `Build-Scripts.ps1 -Validate` runs the same gate.

Translations are reviewed and merged as normal commits — the project does not
use a translation bot, so there are no bot-authored commits in the history.

### Roadmap and Research

`ROADMAP.md` and `RESEARCH.md` are maintained by the project owner. You are welcome to suggest items via issues or discussions, but please do not directly modify the researcher queue sections in pull requests.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](../LICENSE).
