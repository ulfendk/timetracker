-- Adds optional start/end/break metadata for a TimeEntry. DurationMinutes stays the stored,
-- authoritative value (every summing/aggregation query keeps working unchanged) - these columns
-- are purely entry/display metadata, computed into DurationMinutes at save time. Nullable so
-- existing rows (which only ever had a plain duration) remain valid, displayed as duration-only.
ALTER TABLE TimeEntry ADD COLUMN StartTime TEXT;
ALTER TABLE TimeEntry ADD COLUMN EndTime TEXT;
ALTER TABLE TimeEntry ADD COLUMN BreakMinutes INTEGER;
