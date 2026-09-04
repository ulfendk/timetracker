# Changelog

## 0.3.2

The day view's logged-time list now has an edit button, not just delete - it loads the entry
back into the "Log time" form (project, start/end, break, note) so you can correct a mistake
instead of deleting and re-adding it.

## 0.3.1

Each day card in the week view now shows a small weekday indicator (7 boxes, the current day's
box colored in) alongside its icon. Fixed a bug where clicking the edit button on a week-view day
card, or "back to today" from the day view, 404'd when running behind Home Assistant's ingress
proxy - both used an absolute link instead of one relative to the app's ingress path. Also fixed a
crash if the dark-mode preference stored in the browser could no longer be decrypted (e.g. after
`/data` is wiped) - it now just falls back to the system preference instead of taking the page
down.

## 0.3.0

Week view redesigned as a card list (icon, hours, weekend/day-off status, an edit button to jump
straight into that day), with a month divider when a week spans two months. The Today page now
doubles as a day-editor for any date, and shows this week's numbers alongside last week's. Month
view shows a nominal-days count next to nominal hours. Days off supports adding a date range in
one go (weekends and already-recorded days are skipped automatically) and collapses consecutive
same-type entries into one row with a total day count. Added a dark mode toggle, defaulting to the
system setting. Added a Danish translation with a language switcher, which also fixes decimal
numbers to use a comma in Danish (both for display and for typing them into the Weekly hours
field).

## 0.2.0

Nominal-hours settings, clients, and projects can now be edited (not just added). Time is logged
via from/to time pickers with a break-minutes field instead of a plain duration. Date pickers use
a Monday-start week with week numbers. New pages: Billing (hours per client/project for a date
range), Days off (add/edit/delete a day off for any date, with a type - Sickness, Vacation,
Official holiday, or Forced time off), Distribution (a chart of actual vs. nominal hours per
weekday), and Data (export/import using Track My Time's own JSON format, with a conflict preview
before applying, plus a one-time conversion flow for importing another time tracker's export).
MQTT now also publishes year-to-date sick/vacation day counts, and reconnects automatically with
backoff if the broker isn't up yet at startup or drops mid-session - previously either case
disabled MQTT publishing for the rest of that run.

## 0.1.1

Fixed a startup crash when an MQTT broker is configured: the periodic MQTT publish loop used a
`PeriodicTimer` in a way that could throw `InvalidOperationException` and take the whole app
down, essentially always on first run. Also fixed `config.yaml` incorrectly referencing an
unpublished `image:`, which made Supervisor try (and fail) to pull from GHCR instead of building
locally.

## 0.1.0

Initial release: daily/weekly/monthly time tracking by client and project, nominal-vs-actual
hours comparison, days off, and MQTT Discovery entities for Home Assistant dashboards.
