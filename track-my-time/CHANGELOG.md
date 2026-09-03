# Changelog

## 0.1.1

Fixed a startup crash when an MQTT broker is configured: the periodic MQTT publish loop used a
`PeriodicTimer` in a way that could throw `InvalidOperationException` and take the whole app
down, essentially always on first run. Also fixed `config.yaml` incorrectly referencing an
unpublished `image:`, which made Supervisor try (and fail) to pull from GHCR instead of building
locally.

## 0.1.0

Initial release: daily/weekly/monthly time tracking by client and project, nominal-vs-actual
hours comparison, days off, and MQTT Discovery entities for Home Assistant dashboards.
