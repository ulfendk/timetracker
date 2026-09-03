-- Categorizes a DayOff (Sickness/Vacation/OfficialHoliday/ForcedTimeOff). Nullable - existing
-- rows have no knowable historical category, and Today.razor's quick toggle stays a fast
-- no-type shortcut, so null legitimately means "day off, unspecified type" rather than being
-- backfilled with a guess.
ALTER TABLE DayOff ADD COLUMN Type TEXT;
