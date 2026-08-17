# Compliance allowlist

Reviewed like code. Anything not on this list is a bug. CI reports allowlist size as a metric that is expected to shrink.

Starting set from Caprica (`Orvid/Caprica` @ `e4dee08`):

1. The CK allows implicit coercion from `None` to `Bool` and `String` as long as it is not a literal `None`. We do not allow implicit coercion from `None` to `Bool` or `String` at all.

2. The CK allows implicit coercion from `None` to arrays and objects regardless of origin. We only allow implicit coercion to arrays, objects, structs, and `Var` when the source is a literal `None`.

3. The CK generates direct reads of the underlying variable for `Auto` properties defined in parent classes. This is not safe; we only do this for `Auto` properties defined on the current class.

4. The FO4 CK appears not to check whether parameter names conflict with names in a parent scope. Previous CK versions did; `RobotSelfDestructScript.frenzyFX` is a victim. We check, and this script is allowlisted.
