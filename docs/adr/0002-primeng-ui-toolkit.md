# ADR 0002: PrimeNG Application UI Toolkit

Date: 2026-07-19

## Status

Accepted; supersedes the Angular Material/CDK UI-toolkit decision in ADR 0001.

## Context

ADR 0001 selected Angular Material/CDK 22 because PrimeNG 22 was not production-stable at the original foundation baseline. PrimeNG 22.0.0 is now stable, and the Product Owner has approved it as the sole application UI component library. The foundation must preserve the calm bilingual shell without adding Milestone 1 business behavior.

## Decision

- Use exact production versions `primeng@22.0.0`, `@primeuix/themes@3.0.0`, and `primeicons@8.0.0`.
- Use PrimeNG Styled Mode with Aura as the base preset and a custom `KalmPreset` created with `definePreset`.
- Map Kalm primitive, semantic, surface, form-field, focus-ring, border-radius, and component tokens through the preset. Kalm design tokens remain the single source of truth.
- Remove Angular Material and the direct Angular CDK dependency completely. The application has no direct CDK use; PrimeNG 22 itself declares `@angular/cdk` as a required transitive peer-compatible dependency, so npm retains CDK only inside the resolved dependency graph.
- Do not combine PrimeNG with PrimeFlex, Tailwind, Bootstrap, or another CSS framework.
- Do not use PrimeNG's Material preset or `::ng-deep` customization.
- Keep accessibility fixtures in test-only compilation. Do not expose component showcases through production or normal development routes.

## Consequences

- Existing Material selectors, imports, provider assumptions, packages, and tests are removed. Application source must not import CDK; its transitive installation is owned solely by PrimeNG 22.
- The shell continues to be standalone, strict, zoneless, bilingual, keyboard accessible, and touch friendly.
- PrimeNG theme upgrades must preserve the Kalm token contract and be visually verified in both RTL and LTR directions.
- This amendment changes only the Milestone 0 foundation stack; Milestone 1 remains unimplemented.
