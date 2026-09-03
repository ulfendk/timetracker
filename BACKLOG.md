# Backlog

Notes for resuming work on Track My Time. Nothing here is implemented yet - each item below is
what was asked plus enough context (current state, relevant files, open questions) to pick it up
efficiently later, not a design doc.

## 1. Date picker should respect first-day-of-week (DK: Monday)

`Week.razor`/`DateRanges.WeekContaining` already hardcode a Monday-start week for the app's own
week/month logic. MudBlazor's `MudDatePicker` has its own separate default (Sunday) that isn't
currently aligned with that. Check `MudDatePicker`'s `FirstDayOfWeek` parameter (`DayOfWeek?`) -
if just hardcoding `DayOfWeek.Monday` there is enough, that's the simplest fix and needs no new
setting. Only add a Settings toggle if a genuine need for a non-Monday start shows up.

## 2. Nominal hours should be editable, not just creatable

`INominalHoursRepository`/`NominalHoursRepository` only have `CreateAsync`/`GetAllAsync` - no
`UpdateAsync`/`DeleteAsync`. `Settings.razor` only has an "Add" form. Needs repository methods
plus edit/delete UI on the existing table. Watch for the `EffectiveFrom UNIQUE` constraint in the
migration when editing a date to collide with an existing row.

## 3. Hour input via time pickers (from/to) instead of a duration field

`TimeEntry` currently stores only `DurationMinutes` (see `Models/TimeEntry.cs`, migration
`0001_Initial.sql`). Moving to from/to times needs a schema decision: add nullable
`StartTime`/`EndTime` columns alongside duration (computed on save), or replace duration
entirely and derive minutes from the pair. A new migration (`0002_...`) either way. Open
question for when we resume: keep plain duration entry as an alternative, or fully replace it?

## 4. Clients and projects should also be modifiable

Partially already there: `ClientRepository`/`ProjectRepository` already have `UpdateAsync`. The
gap is purely UI - `Clients.razor`/`Projects.razor` only expose add + archive/restore, no edit
form for renaming a client/project or reassigning a project's client.

## 5. JSON import

User will provide a sample JSON format when we resume - don't guess a schema ahead of that. Once
we have it: needs an import page/flow, validation, and a decision on conflict handling (match
existing clients/projects by name vs. always creating new ones), ideally in one transaction.

## 6. JSON export

Dump `Client`/`Project`/`TimeEntry`/`DayOff`/`NominalHoursSetting` tables to JSON. Blazor Server
can't trigger a browser file download from C# alone - needs either a minimal API endpoint that
streams the JSON (simplest) or `IJSRuntime` interop. Worth designing the export shape to double
as the import format from #5.

## 7. Billing page: hours to bill per client/project

New page - pick a billing period (date range), group `TimeEntry` by client then project, sum
hours. `Month.razor`'s existing "by client" grouping (`TimeEntryRepository.GetByDateRangeAsync`
+ `GroupBy`) is the pattern to extend down to per-project. No hourly-rate/invoicing concept exists
yet - scope is just "how many hours per project," not computing amounts, unless that's wanted
when we resume.

## 8. Week numbers in the date picker, if natively supported

Check whether `MudDatePicker` has built-in week-number display before doing anything - user was
explicit: **do not build this ourselves** if it's not natively supported. Lower priority/nice-to-have.

## 9. Planned time off in the future

`DayOff`/`DayOffRepository` already allow any date, past or future - no code restricts this today.
The actual gap: `Today.razor`'s day-off toggle only ever operates on *today's* date. There's no
page to add/view/remove a day off for an arbitrary future date. Needs a dedicated days-off
management UI (extend Settings, or a new page) listing upcoming days off with add/remove.

## 10. Switch to prebuilt GHCR images

`config.yaml` currently has no `image:` field on purpose (see its comment, and the "Releasing"
section in `README.md`) - Supervisor always *pulls* a configured image and never falls back to a
local build on failure, which is exactly what broke the first real-HA install (a 403 from GHCR,
since no version tag had ever been pushed). The CI workflow
(`.github/workflows/build.yml`) already builds and pushes a multi-arch image on `vX.Y.Z` tags;
it's just never been used yet. Once ready: push a tag matching `config.yaml`'s `version`, make
the resulting `ghcr.io/<owner>/timetracker/track-my-time` package public, then add `image:` back
- steps are already spelled out in `README.md`'s "Releasing" section. Worth doing once the app
has settled down some, since local builds are slow on the HA Blue's SoC.

## 11. Expose statistics to HA

From HA, a dashboard with Sick days and vacation days for the year would be nice to have, so the
data should be exported to MQTT if they are not already.

## 12. Equal distribution of nominal hours

I would like graphs showing my distribution of nominal hours vs actual over
a week / month shold also be deducible (e.g. have a graph per weekday, showing whether I work more
or less on Mon, Tue, Wed, etc.)

## 12. Better support for types of days off

Days off should support:
- Sickness
- Vacation
- Official holiday
- Forced time off

## 13. Add screenshots to the repository for presentation on Github and in Home Assistant. Also
include the logo in HA.
