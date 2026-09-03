# Data Trace Suite (DTS)

Data Trace Suite is a solution for [OneStream](https://onestreamsoftware.com/) that lets you capture a snapshot of
your data at a point in time and compare it against another snapshot later - entirely from within OneStream, with no
other tools needed. Results are shown using the names you already know - entities, accounts, scenarios - never
internal system IDs.

## Why you'd use this

- **Before/after a calculation change or performance tuning pass** - snapshot your data before you touch a business
  rule or calc, make the change, snapshot again, and confirm the numbers didn't move (or see exactly where they did).
- **Comparing environments** - snapshot Dev and UAT (or UAT and Prod) and see whether the drivers, actuals, or any
  other data actually match, cell by cell, instead of spot-checking reports by eye.
- **Validating a migration or upgrade** - take a snapshot before an upgrade and one after, and confirm the data came
  through unchanged.
- **Tracking data over time** - keep a history of snapshots and go back to compare any two of them whenever a
  question comes up about what changed and when.

## What it does

- **Create a snapshot** - pull a slice of cube data straight from OneStream, or import one from a CSV file.
- **Compare two snapshots** - pick a baseline and a target, and get a clear list of what was added, removed, or
  changed.
- **Audit history** - a record of who created, compared, exported, or deleted each snapshot, and when.
- **Export / import** - move snapshots in and out as CSV.
- Once a snapshot is taken it can't be edited or added to - it's a fixed record of the data at that moment, so
  comparisons are always trustworthy.

## Getting it

This repository contains the solution's backend. An XML ready-to-upload version is published on the [Releases](../../releases) page.

## Install

Load ApplicationWorkspaces_DTS_xxx.XML under Application - Load/Extract inside your application.

## License

MIT - see [LICENSE](LICENSE).
