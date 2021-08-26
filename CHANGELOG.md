# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [1.0.2] - 2021-08-26
### Fixed
- Double float values not being saved correctly
- Editor prefix caused the registry key value to be new every time because it was placed in OnEnable() resulting in very long product name repeated keys

## [1.0.1] - 2021-08-18
### Added
- Prefix for editor prefs
### Changed
- Package type from library to tool

## [1.0.0] - 2021-07-29
### Added
- First public release