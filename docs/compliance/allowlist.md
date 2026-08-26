# Compliance allowlist

Reviewed like code. Anything not on this list must be byte-identical PEX. Semantic equivalence applies only to these constructs. CI reports allowlist size as a metric that is expected to shrink.

Starting set (published CK differences):

1. `none-to-bool-string` — The CK allows implicit coercion from `None` to `Bool` and `String` as long as it is not a literal `None`. We do not allow implicit coercion from `None` to `Bool` or `String` at all.

2. `none-to-object` — The CK allows implicit coercion from `None` to arrays and objects regardless of origin. We only allow implicit coercion to arrays, objects, structs, and `Var` when the source is a literal `None`.

3. `parent-auto-read` — The CK generates direct reads of the underlying variable for `Auto` properties defined in parent classes. This is not safe; we only do this for `Auto` properties defined on the current class.

4. `parent-scope-params` — The FO4 CK appears not to check whether parameter names conflict with names in a parent scope. Previous CK versions did; `RobotSelfDestructScript.frenzyFX` is a victim. We check, and this script is allowlisted.

## Running

Tier 1 (compile vs committed hashes; skips when the local corpus is absent):

```
papyrusc verify --compile --game skyrimse|fallout4|starfield --source ./corpus/<game> --baseline docs/compliance/baselines/<game>.json
```

`--source` defaults to `./corpus/<game>`. Public CI must not require game files.

Tier 2 (`--ck-compiler`, compile with both papyrusc and a local CK binary) is not implemented yet. See `docs/compliance/baselines/README.md`.
