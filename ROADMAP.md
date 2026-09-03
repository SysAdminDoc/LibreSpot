# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P3: Tests read `SIGNPATH.md`, but `.gitignore:33` ignores it
  Why: `ReleaseArtifactContractTests.ReleaseTrustDocs_DescribeLocalReleaseEvidenceOnly` and `ReleaseTruthTests.SupportAndSigningDocsMatchTheStableReleaseLine` read `SIGNPATH.md` from the repo root, and `SECURITY.md` treats it as the signing decision record, yet the file is gitignored and absent from a fresh clone, so those tests fail anywhere but this machine.
  Acceptance: WHEN the repository is cloned fresh, the release trust tests SHALL find the signing decision record. Either track `SIGNPATH.md` (remove the ignore entry and commit it) or move the decision record into `SECURITY.md` and drop the file reads. Whichever is chosen, the README and SECURITY references point at a tracked file.
  Complexity: S
