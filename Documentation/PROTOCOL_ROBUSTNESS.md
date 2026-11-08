# Network codec boundary validation

## Scope and status

This is an executed cloud validation and small codec correction, not a new mission feature. It changes `LanRangeProtocol`, which is used by the existing range and nested mission packets. It does not wire the inactive mission into Unity or touch materials, scenes, packages, settings, transport endpoints or the user's PC.

Base: `2c1c07abb391d947cd0c8259d5d7775b9fec5ab5`. Current source/result hashes are in `PROTOCOL_ROBUSTNESS_RESULT.json`. The separately pending local floor material candidate remains pinned to `4b7e1ab63201afabd3db172ebc4eaf87447eb1dd`; this cloud result neither replaces that candidate nor authorizes a material save.

## Reproduced defects

### Double-to-float conversion could produce unreadable packets

The encoder validated double-precision input, then wrote IEEE single-precision fields. A value just inside an existing tolerance could round outside it. The receiver correctly rejected the resulting packet, but the encoder had returned successfully. Mission capture relied on that success and could consume a sequence while returning an unreadable packet.

Reproduced witnesses include:

- Fire/reload direction X = `sqrt(0.999) + 1e-12`, Y/Z = 0. The original is inside the near-unit tolerance; the rounded float is outside it.
- Move intent `(1.0004753629146135, 0, 0.0070034419303274965)`. Its rounded squared magnitude exceeds the accepted bound.
- Member or infected position Y = `1.5990000001` with otherwise valid coordinates. It is inside the eye-height tolerance as a double but outside after float rounding.

The correction validates both the original command and its actual float representation before writing bytes. Snapshot validation checks original and float-representable member/enemy positions. Invalid encodes throw `ArgumentException` before producing a packet. Mission capture's existing fail-closed handling returns false/null and preserves its sequence; a later valid pose can produce sequence 1.

No receive tolerance was widened, no host pose was silently clamped, and no caller aim was silently normalized. Shared gameplay pose validation is unchanged: a trusted adapter can still provide a double pose accepted by the gameplay gate but not representable by the wire format, in which case capture refuses it. The adapter must provide a legitimate representable pose, not retry the same invalid aggregate indefinitely. Ordinary Unity-origin float coordinates are already representable. This claim does not replace native acceptance.

### Duplicate connection lifetimes were accepted

The snapshot validator required unique peers and slots, but only required each connection ID to be positive. `LanSession` issues unique monotonically increasing connection lifetimes, so two current members cannot legitimately share one. The validator now also requires connection-ID uniqueness. Direct range snapshots and nested mission frames reject that malformed membership without exposing partial output.

These checks do not authenticate any sender. Transport-derived identity and host-sender checks remain mandatory. No packet field, length, version or handshake changed; valid existing packets retain their format.

## Executed tests

Final unfiltered run: **248/248 pure-core NUnit cases passed**, including all previous 234 cases and 14 new robustness cases. Compilation completed with **0 errors and 0 warnings**. There were **0 skipped, inconclusive or warning cases**.

The new fixture executes:

- 5,000 generated ordinary range commands and 5,000 power commands, including large valid identities/sequences, with decode and byte-stable re-encode assertions.
- 1,200 generated mission frames covering all 60 combinations of six modeled state variants and team sizes 1 through 10, with range and mission round-trip checks.
- All single-bit mutations of fourteen representative mission frames, including two active-revive/downed-teammate seeds: **33,424 mutated packets**.
- **20,000** deterministic raw or valid-header-biased byte arrays, including short and oversized inputs, sent to all four decoders.
- **5,000** stale/duplicate/malformed packets against a terminal client replica, plus separate valid-but-wrong-session and wrong-round frames. Rejected input must leave the accepted object unchanged; the next legitimate sequence is still accepted.
- Explicit float-boundary and duplicate-connection regressions, plus capture recovery without sequence loss.

Accepted arbitrary packets must re-encode byte-for-byte; rejected snapshots/frames must return null and rejected commands must retain zero sequence. Mutations that remain legitimate state are allowed. The tests do not incorrectly require every changed packet to fail.

A corrected pre-fix test run compiled and reproduced **9 failures / 239 passes**, with no test errors. Before that, an initial new generator accidentally placed the revive flag on the X axis instead of its documented Z axis; correcting the test removed that authoring error without changing production behavior. The nine remaining failures then passed after the codec fix. No failing test was skipped or assertion weakened.

Environment: approved Microsoft .NET SDK 10.0.403; .NET runtime 10.0.14; production core in a separate .NET Standard 2.1 assembly; C# 9; NUnit/NUnitLite 3.14.0. Execution UTC: `2026-11-08T19:00:54.3133853Z` to `2026-11-08T19:01:13.6529627Z`, 19.339351 seconds excluding restore/build. Source and package-lock hashes were unchanged during execution.

An independent read-only review confirmed the production correction and suggested broader coverage. The final run includes all phase/team combinations and active-revive mutation seeds. Connection uniqueness adds one bounded set of at most ten entries; it does not add a decode pass.

Static checks also passed: 76 source/assets, nine assemblies, 91 metadata GUIDs, 48 Git-attribute assertions and 15 malformed-importer fixtures. These are separate checks, not Unity results.

## Limits and next native gate

This finite deterministic campaign is not exhaustive verification, a penetration test, proof of authentication, a performance benchmark or evidence of resistance to an unbounded packet flood. It exercises parsers in memory, not sockets, Unity Transport or NGO serialization.

Native compilation, the full current EditMode/PlayMode inventory, Windows build and loopback regression remain unrun for this codec correction. In particular, existing range networking must be revalidated on the exact later approved Unity candidate because this changes its validator. The earlier accepted camera/loopback baseline is historical evidence only. Real LAN and ten-player performance remain unverified.
