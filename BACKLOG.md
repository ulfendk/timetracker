# Backlog

Notes for resuming work on Track My Time. Nothing here is implemented yet - each item below is
what was asked plus enough context (current state, relevant files, open questions) to pick it up
efficiently later, not a design doc.

## 18. Nominal time for a week shows 37.4 instead of 37.5

Investigated but **not reproduced**: `NominalHoursCalculator`/`TimeSummaryService` are `decimal`
end to end (no `float`/`double` in the calculation path), and live-testing with a weekly-hours
setting of 37.5 showed the exact value everywhere it's displayed - `Week.razor`'s plain-text
summary, `Month.razor`'s plain-text summary, and the `Distribution.razor` chart tooltip (hover)
all read `37.5`/`7.5` correctly, not `37.4`. The only `decimal → double` conversions in the app are
the `MudChart` feeds in `Month.razor`/`Distribution.razor`, which didn't misround in testing
either. Needs more specific repro info to pick back up: which page/view showed `37.4`, and what
the nominal-hours settings and any days-off looked like for that week (a mid-week
`NominalHoursSetting` change could legitimately produce a non-`.5` total, which would be
correct-but-confusing rather than a bug).
