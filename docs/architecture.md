# Let's Play Baseball — Architecture

> **Status:** Working draft v0.2 — October 27, 2026  
> **Purpose:** Record current architectural decisions and boundaries before repository bootstrap.  
> **Scope:** Project-wide principles (including deferred reporting and publication systems), Milestone 0 (setup), and Milestone 1 (ball flight). Detailed physical equations, schemas, and API signatures belong in follow-up specifications.

## 1. Vision

*Let's Play Baseball* (LPB) is a modular, physics-grounded baseball simulation and management platform. The long-term vision includes realistic ball and athlete motion, detailed statistical analysis, configurable fictional leagues, historical records, constrained simulations, in-universe journalism/scouting/almanacs, and optional 3D presentation. In-universe media organizations may publish competing analytical methods and distinct editorial interpretations of shared facts.

The first engineering deliverable is much smaller: a standalone, testable, three-dimensional baseball **ball-flight simulator** that requires neither Unity nor a database to run.

**Scope rule:** Do not implement a future subsystem merely because the architecture reserves a place for it.

## 2. Working principles

1. **Headless by default.** Authoritative simulations must run without a renderer, interactive UI, or database service.
2. **Simulation-authoritative outcomes.** Gameplay-relevant physical states, movement, and collisions are determined by simulation systems, not by the frame rate or appearance of an animation.
3. **Motion is not rendering.** Bat paths, glove reach, human kinematics, and collisions may become part of the simulation. Animation and IK can visualize or inform those models, but rendering is not required to evaluate them.
4. **Explicit contracts, loose coupling.** Subsystems communicate through documented interfaces and data formats. They may depend on clearly defined capabilities, but not on implementation details of other modules.
5. **Controlled simulation time.** Numerical integration advances according to simulation time, independently of Unity's frame loop. Event handling and interpolation must have documented numerical behavior.
6. **Reproducibility and verification.** Control inputs, random seeds where relevant, model versions, and numerical settings. Automated tests should distinguish software correctness from empirical validity. Cross-platform numerical results may require tolerances rather than bit-for-bit equality.
7. **Progressive fidelity.** Physics models and visual detail can improve separately where their contracts permit. A new rendering style must not silently change an already simulated play.
8. **Historical integrity.** Historical facts, revisions, reconstructed events, alternate timelines, and generated but uncommitted scenarios must remain distinguishable.
9. **Preserve independent tools.** Existing subprojects, including the Python baseball-name-generator, need not be rewritten to match LPB's primary implementation language.
10. **Separate facts, estimates, and narratives.** Canonical events and historical records are distinct from derived metrics, subjective scouting evaluations, statistical claims, and published prose. All outputs should be traceable to their data, assumptions, and model versions.
11. **Support analytical pluralism.** Multiple fictional organizations may define and publish different defensible metrics or projections from the same observations. Methodology, baselines, calibration, and versioning must be explicit, not hard-coded as one universal rating.

These are working principles subject to revision when prototypes reveal better approaches.

## 3. Conceptual subsystem boundaries

| Subsystem | Responsibility | Immediate status |
| --- | --- | --- |
| **Physics** | Physical state, forces, numerical integration, trajectories, and eventually collision mechanics | **Implement in M1** |
| **Athlete motion and kinematics** | Physical body, bat, glove, and movement constraints; not the graphical skeleton | Future |
| **Decision-making and baseball rules** | Swing decisions, fielding choices, game rules, league/game flow | Future |
| **Scenario and historical management** | Conditional simulation, historical import/revision, reconstruction, provenance, alternate timelines | Future |
| **Persistence** | Canonical saved game state and historical records; SQLite is the leading candidate | Future |
| **Analytics and metric models** | Queries, conventional and competing custom metrics, model fitting, historical baselines, and versioned evaluations; DuckDB/Parquet are leading candidates | Future |
| **Statistical discovery** | Structured searches for records, streaks, rare events, and defensible situational trivia | Future |
| **Reports and publishing** | Verified facts for game recaps, analytical articles, scouting reports, rankings, biographies, team/season almanac pages, and export | Future |
| **In-universe organizations and authors** | Organizations, analyst identities, methodological affiliations, editorial voice, publication history, and canonical identities | Future |
| **Presentation** | Unity UI, 3D models, skeletal animation, audio, replays, and visual interpolation | Future |
| **External generators** | Names and other independently developed content generators accessed through contracts | Existing name generator; LPB integration later |

This is a **logical** decomposition, not a decision to create eight repositories or network services. Prefer ordinary libraries, clean interfaces, and lightweight adapters until demonstrated requirements justify more complexity. Multiple rows may be logical modules within one library or application; this table does not mandate separate services.

### Physical motion versus graphical animation

A batter's decision may produce a swing-control plan; a kinematic model may produce the bat's physical path; a collision solver may produce contact and batted-ball state. Unity may then display that outcome. The headless simulation still requires meaningful physical geometry and movement constraints; it simply does not need rendered animation.

For future replay support, record sufficient authoritative state, events, timestamps, and version/provenance information. Exact replay-storage detail is deferred.

### Facts-to-publication boundary (future)

The long-term flow should be: **canonical games/history → documented statistical queries and model versions → validated facts/claims → article or almanac content → presentation/export**. An authoring system may select, contextualize, and narrate findings; it must not invent *canonical* events or silently overwrite source records. In-universe authors may speculate, disagree, repeat rumors, or even be demonstrably wrong, but the system should distinguish their published claims and knowledge-at-the-time from verified world facts.

The historical/scenario subsystem owns revisions and timeline provenance, analytics owns computations, reporting owns narratives and publications, and presentation owns how content appears. SQLite or DuckDB are infrastructure choices, **not** themselves engines for reporting, back-simulation, or prose generation.

## 4. Milestone 1 — Ball Flight Simulator

**Goal:** Given initial conditions and an environmental model, compute a physically grounded 3D baseball trajectory and report measurements, with controlled numerical error and documented scientific assumptions.

### In scope

- Baseball position, linear velocity, spin vector, and relevant physical parameters.
- Gravity, aerodynamic drag, and a calibrated spin-dependent lift/Magnus model.
- A uniform atmospheric model using a consistent treatment of temperature, pressure, humidity, and altitude; configurable constant wind.
- Numerical integration, trajectory samples, termination at a flat ground surface, and derived measurements (e.g., flight time, maximum height, ground-impact position).
- Headless command-line experiments, structured outputs, automated tests, and reference comparisons.
- Documented uncertainties and applicability limits of aerodynamic coefficients.

### Out of scope for M1

Bat-ball contact, swing generation, athlete/fielding dynamics, wall or ground bounces, injuries, baseball game rules, statistical databases, Unity scenes, detailed stadium geometry, precise seam-shifted-wake modeling, and automated journalism/almanac generation. The design should leave room for future advanced aerodynamics without requiring it now.

### Provisional scientific and numerical conventions

- Internal units: SI (meters, seconds, kilograms, radians); display conversions only at input/output boundaries.
- Internally use double-precision vectors and numerical quantities.
- Preliminary field coordinates: right-handed with **+X** from home plate toward center field, **+Y** toward left field, **+Z** upward. Confirm this before implementation.
- Represent spin as an angular-velocity vector in radians/second; convert from rpm and axis descriptions at the boundary.
- Begin with fixed-step fourth-order Runge–Kutta (RK4), compare against an analytic gravity-only trajectory, and test convergence as the step decreases.
- Force calculations should be modular (gravity, drag, spin-dependent lift) and accept physical state and environmental conditions rather than engine objects.
- Identify the ground intersection more precisely than simply stopping at the first time step below ground (e.g., interpolate/refine the crossing).
- Atmospheric inputs must not double-count altitude and pressure. Document how density and other properties are derived.

The final equations, coefficient choices, timestep, environmental formulas, and physical constants will be selected and cited in a separate M1 physics specification, not assumed final here.

### M1 phases and exit tests

1. **Scientific specification:** With M0 infrastructure already available, finalize units/coordinates, equations, force-model conventions, and reference test cases.
2. **Gravity-only motion:** Numerical trajectories agree with analytic projectile-motion solutions within declared tolerances; event detection works.
3. **Aerodynamics:** Drag, spin-dependent forces, wind, and atmosphere are configurable and independently testable.
4. **Verification and validation:** Numerical convergence, unit tests, parameter sanity checks, and comparisons with published baseball models/data are documented. Reference agreement is evaluated with matching assumptions rather than assumed.
5. **Usable prototype:** Run a scenario without graphics; export machine-readable trajectory results; plot or inspect comparisons; document limitations and future integration points.

**Definition of done:** A clean checkout can build, run the headless CLI, pass automated tests, and reproduce documented reference experiments within stated numerical tolerances. A visual game is *not* required.

## 5. Long-term reporting, statistical discovery, and in-universe knowledge (deferred)

**Status:** Architectural placeholder only. This is part of the intended LPB experience, but **not** an M0 or M1 implementation requirement. Detailed report types, schemas, interfaces, and tooling belong in a future design specification and a dedicated reporting workstream.

### 5.1 Statistical discovery and situational trivia

The system should support composable queries over canonical historical events and aggregates, including streaks, first/last occurrences, records, combinations of game conditions, and unusual comparisons. A future discovery component may actively propose potentially interesting claims (e.g., “first player since YEAR to accomplish X under conditions Y”), then verify them with a deterministic query against the appropriate historical scope.

Design expectations for future work:

- Specify **who/what, event, filters, dates, grouping, and comparator** as a structured claim, rather than constructing unsupported trivia directly in prose.
- Account for incomplete history. **No previous recorded instance** is not the same as **first ever** when older seasons lack detailed play-by-play data.
- Record the supporting query, available time span, data quality, source/timeline version, and exact results so claims can be audited and retested after revisions.
- Rank interesting findings by rarity, narrative context, and relevance without treating arbitrary cherry-picked filters as inherently meaningful. Report uncertainty and selection effects where appropriate.
- Support conventional tables, interactive filters, charts/heatmaps, export, and eventually rendered in-universe story cards through presentation adapters.

### 5.2 Competing metrics and fictional analytical institutions

Different fictional websites, analysts, clubs, scouting services, or research groups may publish rival estimates of pitch quality, player value, projections, or talent. Their measurements should use common canonical inputs where possible, while allowing **distinct feature sets, target variables, normalization, weights, priors, and model versions**.

Each analytical methodology should eventually have a definition describing its owner/publisher, name, model version and effective dates, inputs and required data coverage, training or calibration assumptions, league/season comparison population, output scale, limitations, and provenance. Results should identify the exact methodology version applied. Organizations may change their methods over time; historical published values may stay archived while a modern recalculation is presented separately.

For example, fictional **StatLab wxPITCH**, **Diamond Analytics dPitch+**, and **Beyond the Boxscore PitchGrade** might all grade pitching differently. These are exploratory *working names*, not approved canon or a commitment to specific formulae. Similar differences can exist between organizations' scouting grades, defensive value estimates, and WAR-like statistics. Normalizations (including a league-average baseline around 100, where chosen) must clearly define the relevant league, population, season, and standard-deviation/scaling convention.

**Research and attribution:** Some real-world metrics have fully published formulae; others publish explanatory features and broad model structure without enough detail to reproduce exact fitted models. FanGraphs publicly describes Stuff+, Location+, and Pitching+ at a conceptual level, including their distinct inputs and goals, but that description alone does not specify a complete, reproducible trained implementation. Consult current primary documentation and licensing/attribution requirements before implementing a real-world metric or using reference data. Distinguish a faithful implementation of a published method from an approximation or LPB-original model; do not label an approximation as the official proprietary statistic.

Useful background: [FanGraphs' Stuff+/Location+/Pitching+ primer](https://library.fangraphs.com/pitching/stuff-location-and-pitching-primer/) and [FanGraphs' comparison of WAR approaches](https://library.fangraphs.com/war/differences-fwar-rwar/).

### 5.3 Journalism, scouting, and report generation

Reports may include game recaps, analytical/sabermetric essays, prospect profiles, scouting evaluations, rankings, team previews, historical retrospectives, and editorial opinion. Different in-universe authors and publications may have distinct vocabulary, preferred statistics, coverage priorities, confidence thresholds, methods, and biases. **Editorial voice** and **analytical model** are separate properties: writers can cite a publisher's methodology, challenge it, or compare competing ones.

A proposed, replaceable pipeline is **retrieve/query → assemble a structured evidence bundle → select a report type and author/publisher → draft (templates and/or optional language generation) → validate factual claims → publish/export**. Rendered prose is a derivative artifact, not the source of game truth. Numerical claims, standings, dates, attributed quotes, and claimed historical firsts should be grounded in sources or represented as attributed in-universe claims, rumors, or inventions—not silently promoted to canonical truth. Tool choice for prose generation remains open; a language model must not become a required dependency of the baseball simulation.

### 5.4 Almanacs, encyclopedic pages, and visual outputs

The reporting system should also support stable, linkable pages for players, teams, leagues, stadiums, seasons, records, and notable events, including fake-encyclopedia/Wikipedia-style biography and history pages. Pages may mix structured infoboxes, timelines, narrative sections, statistics, charts, images, and internal links. Favor structured underlying content with rendering adapters (e.g., Markdown/HTML, exportable almanacs, and eventually Unity dashboards) over binding the article model to any one visual interface.

Use stable entity identifiers and revision-aware references. Historical changes should invalidate affected computed claims, flag affected generated pages for review or regeneration, and preserve intentionally archived *as-published* articles when appropriate. A retrospective may describe what a writer believed in a particular year; a modern encyclopedia entry may show the latest canonical information. The system must distinguish those perspectives rather than silently rewriting old journalism.

### 5.5 Deferred integration boundaries

- **Canonical history and scenario management:** owns game facts, provenance, timeline branching, and reconstruction requests; it does not write articles.
- **Metric/analytics layer:** computes queries, derived values, projections, and methodological comparisons; it does not invent game facts.
- **Discovery and reporting layer:** identifies defensible stories, assembles evidence, and generates or edits publications.
- **Presentation/export layer:** displays dashboards, visual articles, almanac pages, and printable/exportable reports without changing their source claims.
- **Future organizational simulation:** may create fictional publishers, authors, institutional preferences, and changing research agendas; this does not require an early simulation of the entire media industry.

No detailed implementation backlog should be created for these systems until the core simulation and data requirements are mature enough to support credible reports.

## 6. Technology choices (current intentions)

- **Physics implementation:** C# library targeting **.NET 10** for M0/M1, independent of Unity and other engine-specific dependencies. Revisit retargeting, multi-targeting, or an adapter when Unity integration becomes an active requirement; do not assume a .NET 10 assembly can load directly into Unity.
- **CLI and automated tests:** Target **.NET 10**, using the repository-pinned SDK; NUnit with VSTest-compatible tooling for headless automated tests.
- **Editor:** JetBrains Rider preferred, or VS Code; Git/GitHub for version control.
- **Experiments/plots:** Python and common numerical/plotting libraries as needed; these are supporting tools, not runtime dependencies of the physics library.
- **Later presentation:** Unity for 3D visualization and interaction, through an adapter rather than embedded physics authority.
- **Later data:** SQLite for canonical transactional state; DuckDB and possibly Parquet for analytical workloads. Neither belongs in the M1 runtime merely to prepare for future features.
- **Existing generator:** Keep `baseball-name-generator` in Python and independent. A future versioned JSON/CLI-style integration can carry names, profile metadata, and seed/provenance information.

## 7. Milestone 0 — Minimal project setup

**Purpose:** Prepare only the development infrastructure required to deliver M1. M0 contains no baseball force-model implementation.

1. Review this draft, marking accepted principles versus open decisions.
2. Create the `lets-play-baseball` GitHub repository once the document is approved; commit the initial architecture documentation.
3. Create an LPB-wide **GitHub Project** that can track this repository and the existing name-generator repository. Start with a backlog table and a small active-work board.
4. Set up a repository-native `M1 — Ball Flight Simulator` milestone and a manageable issue backlog. Cross-repository project fields can represent broader LPB phases or targets.
5. Verify/install Git, the .NET SDK, and a C# editor; do not install Unity, Blender, SQLite, or DuckDB just for M0.
6. Bootstrap a solution containing a physics library, headless CLI, and test project; add a minimal CI build/test workflow.
7. Confirm the project builds and tests locally and in CI, then begin the M1 scientific specification and implementation.

**M0 completion criterion:** An accessible repository and tracking board, an agreed initial architecture, a working local C# solution, and passing automated CI. Detailed physics work begins only after that foundation is operational.

### Suggested initial repository layout

```text
lets-play-baseball/
├── src/
│   ├── Lpb.Physics/
│   └── Lpb.Cli/
├── tests/
│   └── Lpb.Physics.Tests/
├── research/
├── docs/
│   ├── architecture.md
│   └── physics/
├── .github/workflows/
├── README.md
└── LPB.slnx
```

`docs/architecture.md` is the proposed authoritative location; avoid maintaining duplicate copies.

## 8. Deferred design decisions

- Final coordinate convention, reference frame for pitched balls, and API units.
- Scientific source selection and empirical drag/lift coefficients; spin decay and seam effects.
- Fixed versus adaptive integration and impact-event interpolation tolerances.
- Serialization schemas, replay granularity, and strictness of cross-platform reproducibility.
- Later collision, biomechanics, statistical, history, and scenario interfaces.
- Statistical-discovery query/claim schema, editorial evidence bundles, fictional organization/authorship model, publication templates and rendering, and report invalidation/versioning policies.
- Long-term repository topology and Unity integration strategy.

Record major decisions and their rationale in short design notes or Architecture Decision Records (ADRs) as they arise. Avoid treating provisional choices as permanent merely because they appear in this document.

## 9. Workstream and conversation routing

Chats are **working contexts**, not substitutes for durable repository documentation. The architecture, decision records, issue tracker, and tests should remain authoritative even when a discussion moves to a new chat. Do not split conversations merely to create organizational symmetry; open or switch chats when their work becomes active and specialized.

Suggested LPB ChatGPT workspaces:

| Chat/workstream | Use when |
| --- | --- |
| **LPB — Architecture & Roadmap** | Cross-system decisions, milestone boundaries, GitHub planning, architecture-document revisions; this is the current chat. |
| **LPB — Physics & Simulation** | Ball-flight equations, integration, biomechanics interfaces, calibration, and physics test design; start when the M1 scientific specification begins. |
| **LPB — Database & Statistics** | Historical schemas, statistical definitions, analytical querying, SQLite/DuckDB integration; start when that implementation becomes active. |
| **LPB — Almanac & Reports** | Trivia discovery, rival in-universe statistical methodologies, journalists/personas, scouting reports, encyclopedia pages, and publication formats; use for substantive report-design discussions, without pulling implementation into M1. |
| **LPB — Game Logic & AI** | Baseball rules, management AI, personnel development, team operations, and organization-level behavior. |
| **LPB — Graphics & Animation** | Unity presentation adapters, rigging, IK, visuals, cameras, export layouts. |
| **LPB — Learning & Research** | Standalone explanations and exercises in C#, numerical methods, baseball physics, statistics, SQL, graphics, and related concepts; use alongside implementation without burying architecture discussions in tutorials. |
| **LPB — Implementation & Codex** | Scoped code change plans, repository edits, test runs, PRs, and implementation review, with Codex/Work for the actual tool-based work. |

**Routing reminders:** At each transition from architectural discussion to specialized physics or reporting research, suggest the corresponding chat. When a task requires local code/files, command execution, a repository edit, or PR review, suggest Codex or Work and prepare a scoped implementation prompt. When a substantial concept needs learning before implementation, suggest **Learning & Research** and return to the implementation chat with the outcome. Users need not open every suggested chat immediately.

**Current next actions:** Review this architecture revision, then establish the LPB repository and cross-repository GitHub Project in M0. Implement no reporting-related code at this stage.
