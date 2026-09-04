import { readFileSync } from "node:fs";
import { resolve } from "node:path";

/**
 * pnpm runs a dependency's install scripts only when the workspace allows that
 * package by name. The 2026-08-04 supply-chain wave shipped its payload in a
 * `preinstall` hook, so the allowlist is a real boundary, and nothing tested
 * that it stays exact: one edit re-enables install scripts for every dependency
 * in the tree.
 */
const WORKSPACE = resolve(import.meta.dirname, "../pnpm-workspace.yaml");

const REVIEWED = ["@parcel/watcher", "esbuild"];

describe("pnpm build-script allowlist", () => {
  it("names only the two reviewed packages", () => {
    const yaml = readFileSync(WORKSPACE, "utf8");
    const section = /^allowBuilds:\r?\n((?:[ \t]+.*\r?\n?)*)/m.exec(yaml);
    if (section === null) {
      throw new Error("pnpm-workspace.yaml must declare allowBuilds.");
    }

    const allowed = [...section[1].matchAll(/^\s+'?([^':\s]+)'?\s*:\s*(\S+)/gm)]
      .filter(([, , value]) => value.replace(/,$/, "") === "true")
      .map(([, name]) => name)
      .sort();

    expect(allowed).toEqual([...REVIEWED].sort());
  });

  it("does not enable install scripts wholesale", () => {
    const yaml = readFileSync(WORKSPACE, "utf8");
    // Any of these turns the allowlist off for the whole tree.
    expect(yaml).not.toMatch(/^\s*(neverBuiltDependencies|dangerouslyAllowAllBuilds)\s*:/m);
    expect(yaml).not.toMatch(/^\s*allowBuilds\s*:\s*true\s*$/m);
  });
});
