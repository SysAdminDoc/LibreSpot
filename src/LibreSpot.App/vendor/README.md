# Vendored source parts

This directory keeps pinned upstream files that the engine implementation draws from. Line endings and trailing whitespace may be normalized for this repository. The files are pinned to the revisions in [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md). Generated bundles do not load this directory directly. Build-time adapters under `src` select the needed behavior and keep LibreSpot's state model consistent.

The unlicensed projects listed in the notices are not present here. Their interaction patterns were re-created independently.
