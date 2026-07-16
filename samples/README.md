# Samples

Drop-in example files for trying out LitmusAI features locally.

## `prompts.json` — bulk prompt import (spec [#1.6](../specs/archive/1.6.md))

A ready-to-import bundle for the **Prompts → + Import prompts** action. Four prompts covering the
full shape: multiple versions with labels, an alternate target model, and a prompt with no versions
(a valid shell you fill in later).

**Format** — a JSON array of prompts; each prompt has:

| field | required | notes |
|---|---|---|
| `name` | yes | prompt name |
| `description` | no | free text or omitted |
| `versions` | no | array; omit or `[]` to create just the prompt shell |
| `versions[].content` | yes (if a version) | the prompt text |
| `versions[].targetModel` | yes (if a version) | e.g. `claude-sonnet-5`, `claude-opus-4-8` |
| `versions[].label` | no | a short version label |

Prompts import into the **organization + folder currently in view**, sequentially, with a per-row
success/error report — a failing row never stops the others.
