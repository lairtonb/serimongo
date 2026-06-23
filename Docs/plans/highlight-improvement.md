# Tail Highlight Improvement Plan

## Context

Tail inserts can arrive in bursts or short continuous batches. When every row starts
the same highlight animation at the exact receive time, the list can look noisy:
some rows appear out of phase, some feel visually dead, and the batch does not read
as a top-to-bottom cascade.

The highlight should remain neutral, fast to appear, and slower to fade. It should
not communicate OK, warning, error, info, or fatal state.

## Implemented Option: Buffered Batch Cascade

Status: implemented in `4bc7dee tweak(ui): synchronize tail highlight batches`.

Summary:

- Buffer incoming Tail entries in the UI for a short window.
- Sort the flush batch by timestamp, with arrival order as the tie-breaker.
- Insert the whole batch at the top in one update.
- Apply a per-row negative animation delay based on visual position.
- Limit how many rows receive the animated highlight per batch.

Current parameters:

- Flush window: `35ms`.
- Highlight duration: `1.1s`.
- Per-row phase offset: `45ms`.
- Maximum highlighted rows per batch: `24`.

Why this was chosen:

- Keeps the table data update immediate enough for Tail usage.
- Avoids artificially inserting rows one at a time.
- Makes a batch read as a coherent cascade from newest to oldest.
- Requires only frontend changes and preserves server-side Tail filtering.

Tradeoffs:

- Very large bursts still collapse into a dense visual moment after the first
  highlighted rows.
- The animation phase is inferred in the client, not supplied by the server.
- The exact values may still need visual tuning after longer real usage.

## Alternative 1: Queued Row Insertion

Summary:

- Put Tail entries into a frontend queue.
- Insert rows one by one with a small delay between insertions.
- Start the highlight normally for each inserted row.

Pros:

- Produces the clearest visual waterfall.
- Easy to understand visually and technically.
- Does not need negative animation delays.

Cons:

- Data appears later during large bursts.
- Queue management becomes more important under sustained logging.
- Can make Tail feel less realtime when traffic is high.

Best use case:

- If visual rhythm matters more than exact immediate insertion.

## Alternative 2: Lateral Activity Indicator

Summary:

- Replace or reduce the full-row background highlight.
- Animate a slim left border, marker, or pulse on each new row.
- Keep row background mostly stable.

Pros:

- Reduces the perception of colored blocks or bands.
- Works well with dense technical tables.
- Less likely to conflict with level colors.

Cons:

- New rows may be less noticeable than with a full-row overlay.
- Needs careful design to avoid looking like selection state.

Best use case:

- If the current highlight is still too visually heavy after tuning.

## Alternative 3: Animation From Batch Age

Summary:

- Store the batch flush time and each row index.
- Compute animation progress from elapsed time and row position.
- Use CSS variables or generated classes to control opacity/phase.

Pros:

- More deterministic than relying only on `animation-delay`.
- Can maintain a consistent cascade even with render timing variance.

Cons:

- More state and more moving parts in the component.
- Easier to over-engineer for a purely visual affordance.

Best use case:

- If CSS animation delay proves inconsistent across browsers or devices.

## Alternative 4: Server-Side Batch Metadata

Summary:

- Have the server attach batch sequence or ingestion sequence metadata.
- Let the client use this metadata to order and phase highlights.

Pros:

- Gives the client stronger ordering information.
- Can align all clients to the same ingestion order.

Cons:

- Requires backend protocol changes for a visual behavior.
- Adds coupling between SignalR payload shape and UI animation.

Best use case:

- If Tail ordering itself becomes a product requirement beyond animation.

## Recommendation

Keep the implemented buffered batch cascade for now. If more polish is needed,
try Alternative 2 next. It is likely the smallest next step that changes the
visual language without making Tail feel less realtime.
