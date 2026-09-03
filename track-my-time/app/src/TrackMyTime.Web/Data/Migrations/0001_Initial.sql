CREATE TABLE Client
(
    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
    Name     TEXT    NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE Project
(
    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
    ClientId INTEGER NOT NULL REFERENCES Client (Id),
    Name     TEXT    NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    Color    TEXT
);

CREATE INDEX IX_Project_ClientId ON Project (ClientId);

CREATE TABLE TimeEntry
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Date            TEXT    NOT NULL,
    ProjectId       INTEGER NOT NULL REFERENCES Project (Id),
    DurationMinutes INTEGER NOT NULL CHECK (DurationMinutes > 0),
    Note            TEXT
);

CREATE INDEX IX_TimeEntry_Date ON TimeEntry (Date);
CREATE INDEX IX_TimeEntry_ProjectId ON TimeEntry (ProjectId);

CREATE TABLE DayOff
(
    Id   INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT NOT NULL UNIQUE,
    Note TEXT
);

CREATE INDEX IX_DayOff_Date ON DayOff (Date);

CREATE TABLE NominalHoursSetting
(
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    EffectiveFrom TEXT    NOT NULL UNIQUE,
    WeeklyHours   TEXT    NOT NULL
);

CREATE INDEX IX_NominalHoursSetting_EffectiveFrom ON NominalHoursSetting (EffectiveFrom);
