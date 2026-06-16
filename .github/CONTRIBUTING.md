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

### Roadmap and Research

`ROADMAP.md` and `RESEARCH.md` are maintained by the project owner. You are welcome to suggest items via issues or discussions, but please do not directly modify the researcher queue sections in pull requests.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](../LICENSE).
