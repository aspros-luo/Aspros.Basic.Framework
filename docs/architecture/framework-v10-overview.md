# Aspros Basic Framework v10 Overview

## Goal

Framework v10 is an incremental evolution of Aspros Basic Framework toward a modular enterprise application foundation.

## Design Goals

- Clear domain boundaries
- DDD friendly primitives
- Extensible infrastructure abstractions
- Support for modern .NET runtime capabilities
- Keep migration cost controlled

## Architecture Direction

The target architecture contains:

- Domain Kernel
- Application Runtime
- Infrastructure Abstractions
- Persistence
- Messaging
- Observability

## Migration Principle

Existing capabilities are preserved first. Refactoring happens through incremental migration rather than a destructive rewrite.
