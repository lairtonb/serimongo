# Property Actions - Phase 1

## Goal

Add visible actions to property cards in the selected log details sidebar.
Phase 1 only implements `Add to filter`.

## Add To Filter

- Add a small visible icon button in each property card header.
- Use `ionFunnelOutline` from the existing `@ng-icons/ionicons` package.
- Tooltip/title: `Add to filter`.
- Operator is always `=`.
- Supported values for quick filters:
  - string
  - number
- Unsupported values for phase 1:
  - boolean
  - null
  - object
  - array

## Behavior

- Clicking the button adds a property quick filter.
- Examples:
  - `prop.CustomerId = 9414`
  - `prop.Region = "br-south"`
- Adding a filter must not pause Tail.
- If Tail is active, keep tailing and send the updated query to SignalR.
- If Tail is paused/searching, reload search results using the updated query.
- Prevent duplicate filters.
- Show a toast when a duplicate filter is requested.
- Show a toast for unsupported values if the action is triggered somehow.
- `Clear filters` should clear property filters too.

## Quick Filters Panel

- Add a `Property filters` block/chip list under the existing quick filters.
- Each chip displays `key = value`.
- Each chip has a remove button.

## Help Update

- Update the Help drawer to describe property quick filters.
- Document that quick property filters currently support only string/number.
- Document manual LogQL options for properties:
  - `prop.SomeField exists`
  - comparison operators
  - text operators for string values
- Note that there is no `isNull`, `isTrue`, or `isFalse` operator today.

## Phase 2 Later

- Add `Add as column` using `ionGridOutline`.
- Render property columns before `Message`.
- Missing values should render blank.
- Property column values should be constrained with max width and ellipsis.

## Deferred

- Context menu on right click.
- Operators other than `=` for quick filters.
- Boolean/null/object/array quick filter support.
