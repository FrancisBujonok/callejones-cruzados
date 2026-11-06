# Session Subagent Cost

**Session:** `babbd8ca-9d99-48fb-bf73-587bfd890fee`  
**Updated:** 20261106_140002

Subagent spawns this session: **5**

| Agent | Spawns |
|-------|--------|
| narrative-director | 1 |
| level-designer | 1 |
| game-designer | 1 |
| audio-director | 1 |
| art-director | 1 |

Each spawn is a fresh context window that re-reads its own inputs.
To reduce this: lower `modes.review_mode` (`/settings modes.review_mode=solo`)
or `modes.rigor` (`/settings modes.rigor=minimal`).
