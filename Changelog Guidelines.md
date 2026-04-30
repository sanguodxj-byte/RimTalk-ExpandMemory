# Changelog Maintenance Agent Guidelines

## 1. Role & Purpose
As an AI Code Agent, your task is to automatically parse git commit histories and update the `CHANGELOG.md` file. You must strictly adhere to the **Keep a Changelog (v1.1.0)** standard, and map **Conventional Commits** to the appropriate sections.

**Core Principle:** Changelogs are for HUMANS, not machines. Do not blindly dump git logs. You must filter noise, group changes logically, and rewrite technical commit subjects into user-facing release notes.

## 2. File Structure & Formatting Rules

The `CHANGELOG.md` must maintain the following strict Markdown structure:

1. **Title**: `# Changelog`
2. **Description**: A short sentence stating the standard used.
3. **Unreleased Section**: `## [Unreleased]` (Always keep this section at the top, immediately below the description).
4. **Release Versions**: `## [X.Y.Z] - YYYY-MM-DD` (Latest version comes first. Dates MUST use the ISO 8601 format: `YYYY-MM-DD`).
5. **Change Categories**: `### <Category>` (Only include categories that have changes in that version).
6. **Hyperlinks**: All version headers must be linked to their GitHub diff URLs at the bottom of the file.

## 3. Change Categories (Keep a Changelog Standard)

You must classify parsed commits into one of the following exact `### <Category>` headers. **Do not create custom categories.**

*   **`Added`**: For new features.
*   **`Changed`**: For changes in existing functionality.
*   **`Deprecated`**: For soon-to-be removed features.
*   **`Removed`**: For now removed features.
*   **`Fixed`**: For any bug fixes.
*   **`Security`**: In case of vulnerabilities.

## 4. Agent Parsing Logic: Conventional Commits Mapping

When reading the git commit history, use the following mapping logic to translate Conventional Commits `Type` into Changelog `Categories`:

*   `feat` -> **Added** (If it's a new feature) OR **Changed** (If it alters existing logic).
*   `fix` -> **Fixed**.
*   `perf` -> **Changed** (Often rewritten as: "Improved performance for...").
*   `refactor` -> Generally ignore, UNLESS it involves a breaking change or significantly affects the user experience, then put in **Changed**.
*   `revert` -> **Removed** or **Changed** (Depending on context).
*   **BREAKING CHANGE** (in footer or `!` in type) -> Must be highlighted in the relevant section, often triggering a major version bump.

**🚫 DROPPING RULES (Noise Filtering):**
You MUST SILENTLY IGNORE the following commit types unless explicitly instructed otherwise by the human developer:
*   `chore` (e.g., dependency updates, CI/CD tweaks)
*   `style` (e.g., formatting, missing semi-colons)
*   `docs` (e.g., README updates)
*   `test` (e.g., adding missing tests)
*   Commits with `wip`, `fixup!`, or `squash!` prefixes.

## 5. Writing Style Rules for Entries
1. Start each bullet point with a capitalized active verb (e.g., "- Add OAuth2 login" instead of "- added OAuth2 login").
2. Include the Pull Request or Issue number at the end of the line if available (e.g., `(#123)`).
3. If the original commit message is too cryptic (e.g., `fix(ui): div margin`), rewrite it to be human-readable (e.g., `- Fix misalignment of the navigation button on mobile screens`).

## 6. Example Template

Below is the exact template you must enforce:

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- User profile customization options (#45).

## [1.1.0] - 2026-04-05

### Added
- Support for Arabic translation (#42).

### Changed
- Optimize database queries to reduce dashboard loading time by 30% (#40).

### Fixed
- Resolve display glitch when users trigger rapid state changes (#38).

## [1.0.0] - 2026-03-01

### Added
- Initial public release of the system.

<!-- GitHub Links -->
[Unreleased]: https://github.com/YourOrg/YourRepo/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/YourOrg/YourRepo/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/YourOrg/YourRepo/releases/tag/v1.0.0