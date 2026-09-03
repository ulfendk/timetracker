# Changelog

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
