---
name: git-commiter
description: Use when adding or changing git commits in this repo, OR when planning/proposing how a set of changes should be split into commits — keeps the codebase consistent and follows established conventions.
---

## Instructions

This skill applies both when actually running `git commit` and when just proposing/planning a
list of commits (e.g. "here's how I'd split this into commits"). A proposed commit plan must
follow the same rules below — atomic grouping, tests check, and message pattern — not just the
final commit command.

1. When doing or planning git commits, use this skill to generate a commit message that is clear, concise, and follows the established conventions of the codebase.

2. Provide a brief description of the changes you made in your code, including any relevant context or reasoning behind the changes.

3. Check that commits are atomic, meaning that each commit should represent a single logical change to the codebase.

4. Check that code in the commit is tested and passes all relevant tests. If no tests are added prompt user to add tests for the changes made.

5. Ask user for Issue number or relevant ticket ID if applicable, to include in the commit message.
Follow to next pattern: 

"Issue Number. Short description of the change made.

Full description of the change, including any relevant context or reasoning behind the changes."

6. If there is no issue tracker set up yet, or the change isn't tied to any ticket (e.g. initial
   repo setup, scaffolding, chores), skip the issue number rather than blocking on it. Use the
   same pattern without the prefix:

   "Short description of the change made.

   Full description of the change, including any relevant context or reasoning behind the changes."