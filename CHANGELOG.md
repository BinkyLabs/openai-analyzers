# Changelog

## [1.0.0-preview.4](https://github.com/BinkyLabs/openai-analyzers/compare/v1.0.0-preview.3...v1.0.0-preview.4) (2025-11-19)


### Features

* **BOA001:** detect non-constant variables passed to SystemChatMessage ([eaa9f15](https://github.com/BinkyLabs/openai-analyzers/commit/eaa9f1551085625917ef63e138f125818d3c490f))
* detect string concatenation in BOA001 rule ([ec6176e](https://github.com/BinkyLabs/openai-analyzers/commit/ec6176e337a35ae7877535ae7f3a7f37266e8c67))

## [1.0.0-preview.3](https://github.com/BinkyLabs/openai-analyzers/compare/v1.0.0-preview.2...v1.0.0-preview.3) (2025-11-19)


### Features

* adds new rule to suggest having a system message last ([a0c63c3](https://github.com/BinkyLabs/openai-analyzers/commit/a0c63c3e88b81328e3fc1655dfccf4a640f2e4d9))


### Bug Fixes

* do not report constants for string interpolation ([d6bb51d](https://github.com/BinkyLabs/openai-analyzers/commit/d6bb51df592c89e7139126db9212d7748ffa7d1f))

## [1.0.0-preview.2](https://github.com/BinkyLabs/openai-analyzers/compare/v1.0.0-preview.1...v1.0.0-preview.2) (2025-11-19)


### Features

* adds an initial implementation of the BOA001 rule ([e78dd00](https://github.com/BinkyLabs/openai-analyzers/commit/e78dd00ff18b08a4fda424006b67204a05d85694))
* adds support for parts interpolation detection ([17f74b5](https://github.com/BinkyLabs/openai-analyzers/commit/17f74b5a668deecab7d806fecab6da86c15fd7bf))


### Bug Fixes

* addresses warning regarding workspaces reference ([360f6d2](https://github.com/BinkyLabs/openai-analyzers/commit/360f6d2fd2866d2bde6d9957b3f921f71babc463))
* interpolation string detection ([335c8b8](https://github.com/BinkyLabs/openai-analyzers/commit/335c8b88f099ead1a8bab843d80c3a4afb500c77))

## Changelog

## Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
