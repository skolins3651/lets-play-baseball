# Minimum interactive trajectory visualizer

Internal milestone **5.5**, originally tracked by [Issue #19](https://github.com/skolins3651/lets-play-baseball/issues/19) and merged through PR #20, established this local scientific debugger. The scoped follow-up to [Issue #6](https://github.com/skolins3651/lets-play-baseball/issues/6), merged through [PR #23](https://github.com/skolins3651/lets-play-baseball/pull/23), adds gravity-plus-drag experiments using the existing [gravity-only](physics/gravity-only-contract.md) and [drag](physics/drag-model.md) Physics APIs. It introduces no new milestone, historical viewer, export format, or comparison workbench.

## Start and use

Install the exact SDK selected by `global.json` (10.0.403), including its ASP.NET Core shared framework. From the repository root on macOS or Windows PowerShell:

```sh
dotnet run --project src/Lpb.Visualizer/Lpb.Visualizer.csproj
```

Open [http://localhost:5055](http://localhost:5055). Use `-- --port 5056` if the default port is occupied. After the documented Release build, append `--configuration Release --no-build --no-restore` before the final `--`. Stop the process with Ctrl+C. No Node.js, Python, database, frontend package manager, runtime CDN, or additional .NET workload is required. The 3D view needs browser WebGL; the 2D views remain usable if 3D rendering fails.

**Run simulation** executes the captured physical inputs. Editing them shows **Unrun input changes** while the previous trajectory retains its own executed-settings summary. A failed new run keeps the previous result labeled **Last successful run**. Reverting numeric inputs clears the pending indicator; formatting `100` as `100.0` is not a change. Invalid/incomplete edits remain pending.

**Apply density** changes only plotted-point count, using the last successful result's immutable request and captured settings. It never applies pending physical inputs or reruns a lost result. Expired/evicted/replaced results require an explicit Run. Loading, response, and error updates are guarded by operation sequence and active result ID so old responses cannot replace a newer display.

Physical model and termination mode are independent. Aerodynamic controls appear only for **Gravity + drag**. Switching models retains their entered values in the page, but inactive values are excluded from requests, validation, pending-state comparison, and executed settings. Switching back restores those values, including incomplete edits. Reverting the model and its applicable inputs clears pending state. Density changes remain available despite invalid pending physical edits; the captured result supplies its own model, request, and aerodynamic settings.

## Inputs and scientific meaning

| Input | Default | Accepted meaning |
| --- | --- | --- |
| Physical model | Gravity only | Gravity only or gravity + drag |
| Speed | 100 mph | Finite, nonnegative magnitude |
| Elevation | 30° | −90° to +90°, including downward and vertical launches |
| Field azimuth | 45° | −180° to +180°; not restricted to fair territory |
| Initial center height | 3 ft | Ball center above local ground; signed values permitted without contact |
| Ground elevation | 0 ft | Field-coordinate ground Z |
| Radius | 1.45 in | Finite, positive spherical-ball radius |
| Ball mass (drag only) | 145 g | Finite and positive |
| Air density (drag only) | 1.225 kg/m³ | Finite and nonnegative |
| Drag coefficient (drag only) | 0.35 | Finite, nonnegative, dimensionless |
| Gravity | 9.80665 m/s² | Finite, nonnegative magnitude acting along −Z |
| Timestep | 0.01 s | Finite and positive; may exceed duration |
| Duration limit | 10 s | Finite and positive |
| Mode | Contact-aware | Stop at first contact or run unconstrained to duration |
| Display points | 300 | Integer 2–2,000, including both path endpoints |

C# converts mph with `0.44704`, feet with `0.3048`, inches with `0.0254`, and grams by dividing by `1000`. A single converted radius supplies aerodynamic settings and contact/presentation geometry. Position is `(0, 0, groundElevation + centerHeightAboveGround)` in metres. Radius is **not added again**. Gravity, density, coefficient, and time are already SI. Raw and converted applicable inputs must be valid; positive mass/radius conversions lost to zero are rejected. No invalid value is silently clamped; existing representability, resource, and geometry validation still apply, including in unconstrained mode. Extremely small radii absorbed by ground-elevation addition are rejected.

The aerodynamic defaults are illustrative and uncalibrated. The drag model assumes constant mass, spherical radius, density, and coefficient in stationary air, without spin, lift, wind, or variable atmosphere. Zero density or zero coefficient delegates numerically to gravity while retaining **GravityAndDrag** identity and all applicable parameters. Such a result is labeled **drag disabled by executed settings** and always uses drag-model analysis. This is independent of unconstrained mode's **ground contact disabled** label.

With the unchanged default launch and timestep, local macOS verification produced drag contact at approximately **3.947666182518006 s**, maximum center height **19.733622375905163 m** above ground, and 300 numerically evaluated display points without warnings. Default unconstrained drag reached 10 s with the same interval maximum. These are representative observations, not portable bitwise or empirical accuracy guarantees.

The right-handed field frame is +X toward first base/right field, +Y toward third base/left field, +Z upward. Field azimuth 0° is +X, 45° is ideal center field, and 90° is +Y. It is not a batter-relative spray angle and makes no fair/foul ruling.

### Exact cardinal conversion

Exact parsed degree values receive exact sine/cosine pairs: −180° `(0,−1)`, −90° `(−1,0)`, 0° `(0,1)`, +90° `(1,0)`, +180° `(0,−1)`, listed as `(sin, cos)`. Zero speed gives positive-zero velocity components. Exactly vertical elevation gives `(0,0,±speedSI)` regardless of azimuth. Other launches use `vx=s*cos(elevation)*cos(azimuth)`, `vy=s*cos(elevation)*sin(azimuth)`, and `vz=s*sin(elevation)`, with positive zero where mathematically required by an exact cardinal input.

All non-cardinal inputs use ordinary double trigonometry after degree-to-radian conversion. There is no epsilon, near-zero snapping, rounding, or angle normalization. Floating-point conversion can itself round or underflow; it does not promise infinite angular resolution. These rules change only application input conversion, never Physics.

Contact uses the existing exact initial-state rules: penetration is rejected; touching while stationary/downward terminates immediately; touching while upward launches. Cross-unit decimal representations can differ by a floating-point rounding unit; no geometric tolerance or snapping hides that distinction. For a reproducible exact-touch test, radius **1 in** and center height **0.08333333333333333 ft** convert to the same represented length at zero ground elevation.

## Three views and vertical references

All views transform the **same backend-provided XYZ points**. They do not solve motion independently. Lines connect supplied points without splines. Hover reports elapsed time, field XYZ in feet, and ground-relative center height.

| View/reference | Meaning |
| --- | --- |
| 3D | Field X/Y/Z; Z axis labeled **Ball-center Z — field origin (ft)** |
| Side profile | Downrange from launch along captured azimuth versus **Center height above local ground (ft)** |
| Top-down | Field X/Y with equal horizontal scale and foul/center direction references |
| Local ground | 3D Z = ground elevation; side height = 0 |
| Center-contact reference | 3D Z = ground elevation + radius; side height = radius |

The view uses proportional distance scales, including an orthographic default 3D camera. Zero-span axes receive finite padding. Ground/contact references describe geometry, not detected events. Unconstrained paths are dashed, retain below-ground states, and are labeled **Ground contact disabled**; the side view highlights the below-ground region. The tool is schematic and does not represent a stadium.

Launch, maximum, and terminal markers are supplied independently of the sampled display path. **Maximum over simulated interval** may be at launch, an interior apex, or an ascending truncated terminal. Only `GroundContact` completion gets a **Ground contact — ball center** marker and contact data. Other results say **Terminal — requested duration reached**, without an invented landing or future completed-flight time. Export and cloud-sharing controls are disabled.

Nonzero-ground acceptance: ground **10 ft**, initial center **3 ft**, radius **1.5 in** gives launch field Z approximately **13 ft**, contact center field Z approximately **10.125 ft**, and side heights **3 ft** and **0.125 ft**. Ground itself stays at 10 ft field Z / zero side height. Use declared numerical/conversion tolerances. Short duration must show a non-contact terminal; unconstrained continuation must remain visible below the ground reference.

## Integration versus plotted points

The simulator runs once and records its own ordinary integration samples. Display count is separate. For positive actual duration, candidate display times are `FinalTime * (i / (N-1))` with double division; endpoints are exactly zero and final time. Rounded duplicate or non-interior timestamps are skipped. Interior states use `GravityOnlyTrajectory.Evaluate(result, originalRequest, time)` or `GravityDragTrajectory.Evaluate(result, originalRequest, originalAerodynamics, time)` according to the executed result's model identity. Maximum analysis follows the same dispatch, including disabled drag. Immediate contact produces one point regardless of requested density.

If supplementary evaluation cannot safely perform RK4, the display discards that attempted path and uses a bounded, deterministic index-spaced subset of existing recorded samples, including endpoints. The warning retains numerical context. No interpolation conceals a failure. Original samples and terminal states never change. A failed maximum query leaves its value/marker unavailable, without substituting a purported exact sampled apex.

The UI distinguishes integration timestep, original count, requested display count, actual display count, and sampling source. When requested count exceeds original count it shows:

> Additional display points can make the curve appear smoother, but do not guarantee greater numerical or physical accuracy.

This does not rerun or improve the authoritative simulation. Numerical solution error, physical-model approximation, empirical agreement, and plotting fidelity are distinct concerns. Gravity-only motion omits drag, wind, spin, bounce, and rolling; visually plausible motion does not establish real-world baseball accuracy.

## Results, failures, and temporary ownership

Measurements use public Physics APIs separately from rendering: actual elapsed/requested time, completion reason, maximum/time, displacement vector, horizontal displacement vector/magnitude, terminal XYZ, and genuine contact/time. Horizontal displacement is final minus initial XY, not path length. Scalar magnitude uses scaling to avoid intermediate squared-value overflow. Optional extrema, measurements, or display-unit/plot-range failures produce limitations while preserving successful simulation data. An unavailable measurement is not zero.

Required integration, event-localization, and resource failures return no successful partial experiment and preserve the previous run. Physics diagnostics retain component/step/time context. Consistency and damping checks may suggest reducing the timestep; coordinate precision, arithmetic range, and step-budget failures require addressing their specific constraint. The Lab does not adapt timesteps, relax tolerances, retry simulations, or manufacture measurements.

[Issue #22](https://github.com/skolins3651/lets-play-baseball/issues/22)'s separately reviewed Physics correction is implemented locally pending publication. Gravity-only and both disabled-drag paths now require independent occurrence and first-contact timing evidence. A rounded equality, premature crossing, substantially late equality, or unresolvable arithmetic/window can produce a contextual numerical failure. The Lab uses its existing simulation-error boundary (HTTP 422), preserves the prior successful experiment, and does not rewrite inputs or substitute a duration result. Details belong in the [gravity contract](physics/gravity-only-contract.md#independent-occurrence-and-first-contact-timing-issue-22); no UI or renderer redesign is required.

One latest successful experiment is owned by each page's unpredictable UUID tab key, kept only in JavaScript memory. The process retains at most four immutable experiments, expires them after 15 minutes of idle time, checks expiry on lookup, and sweeps once per minute. Successful display access refreshes idle time. A fifth experiment evicts the least recently accessed owner. Successful new runs atomically replace that tab's previous result; failed runs preserve it. Only one simulation/display/deletion operation executes process-wide; busy requests are rejected without queuing.

Physics bounds each result to 100,001 samples, approximately 5.6 MB of sample values; four results retain about 22.4 MB plus metadata. Temporary integration arrays, plotting responses, and browser/WebGL buffers require additional working memory. Display payloads contain at most 2,000 path points plus separate landmarks. Page exit attempts deletion, but expiration is authoritative. Refresh/duplicate tabs obtain fresh owners; shutdown clears retained references and stops cleanup.

There is no scenario database, automatic file saving, browser application storage, cookie session, service worker, telemetry/analytics service, history list, or export. API responses use `Cache-Control: no-store`. Only host lifecycle messages are logged; expected numerical diagnostics go to the owning page rather than persistent logs. Closing a page is not a reliable unload-delivery guarantee.

### Application-private HTTP boundary

- `POST /api/simulations`: `{ "launch": { ...applicable UI launch values... }, "displayPointCount": 300 }`.
- `POST /api/simulations/{opaqueId}/display`: `{ "displayPointCount": 300 }`.
- `DELETE /api/simulations/{opaqueId}`: `{}`. It deletes only that owned result; an old ID cannot delete a replacement.

All three require `application/json`, optional UTF-8 charset, a body no larger than 16 KiB, and exactly one `X-Lpb-Tab-Id`: a nonempty 36-character hyphenated UUID. Valid UUIDs are compared by parsed value. Results require both owner and opaque ID. Requests stay same-origin, with loopback Host filtering and no CORS policy. Explicit foreign/opaque Origins are rejected; absent Origin permits local test clients. Kestrel explicitly replaces inherited endpoint configuration and listens only on localhost. `--port` changes only its numeric port.

`launch.physicalModel` accepts `gravity-only` and `gravity-drag`; omission defaults to gravity-only for existing clients. Explicit null or unknown models reject. Drag requires numeric `ballMassGrams`, `airDensity`, and `dragCoefficient`, including when drag is disabled; missing/null values produce field-specific errors. Gravity-only ignores inactive numeric aerodynamic values and captures them as null. Malformed JSON and wrong JSON types still reject before applicability validation.

Responses contain captured settings/effective SI state, named `modelKind` (`GravityOnly` or `GravityAndDrag`), applicable SI `aerodynamics` (null for gravity-only), points, landmarks, counts, measurements, completion, and warnings. They are not a public versioned export schema. Status categories: **400** input/protocol validation, **403** foreign Origin, **404** unavailable/unowned result, **413** body limit, **415** media type/encoding, **422** authoritative simulation failure/budget rejection, **429** busy, and **500** unexpected application error. Unexpected model/provenance mismatches remain application errors. Successful simulation plus optional limitations is **200**, not false failure or partial Physics success.

## Verification

The normal solution-wide locked restore, Release build, NUnit test, and CLI smoke commands include both test projects. Visualizer tests exercise conversions, cardinal/near-cardinal inputs, both models and modes, aerodynamic applicability, disabled-drag provenance, captured resampling, contact semantics, numerical fallback, optional analysis, immutable results, large source arrays, request limits, HTTP protocol, tab isolation, expiry, LRU, and stale deletion. The published #6 baseline has 468 Physics and 82 Visualizer tests; all Physics tests and existing regression meanings/tolerances remain intact. NUnit/VSTest and integration-test packages are test-only; no additional runtime package or browser-test framework is required.

The separate browser-native state harness is `tests/Lpb.Visualizer.Tests/browser/state-tests.html`. Open that local HTML file in a normal browser; it loads the actual application helper through classic scripts, executes eleven test scenarios, and displays results without network requests or scenario storage. It is outside `wwwroot` and is not published with the application. Its assertions cover pending/reverted edits, aerodynamic applicability/request projection, model switching, captured-request resampling, failure preservation, edits during a run, out-of-order responses, stale IDs/errors, and unavailable-cache recovery. Restricted browsers that disallow local files cannot run this page; report that separately rather than treating backend tests as browser verification.

Manual macOS/Windows checks should run both models in both termination modes, both disabled-drag settings, the nonzero-ground acceptance case, exact vertical and immediate contact, short duration, unconstrained below-ground flight, density changes with pending/invalid model or aerodynamic inputs, reversions, failure preservation, multiple tabs, numerical limitations, and all three Plotly views/hover labels. Check that no sharing/export controls or runtime external resources appear. Windows manual checks and GitHub Linux/Windows CI are separate from local macOS results. For remote Mac acceptance, use Rider's graphical rebuild/tests/run interfaces and the Mac loopback browser; retain genuine PNG evidence and a tested-source fingerprint outside the repository. Record runner/configuration limitations and never substitute baseline evidence for the changed implementation.

## Deferred work

The positional plot payload is model-neutral; model dispatch is deliberately limited to these two Physics models. Future analytical references must be separate labeled datasets; future comparisons must distinguish numerical error from physical/empirical accuracy. A historical viewer may reuse rendering only through a separately authorized historical-data subsystem and persistence rules.

C2C may later change units (Type A) or angle/interpretive conventions (Type B), with batter-relative pull/opposite-field directions requiring handedness. Such presentation changes must not alter authoritative states. No C2C framework, analytical solver, model overlays, comparison storage, or historical subsystem is implemented here. Issues #7–13 retain further physics, numerical verification, empirical validation, CLI/versioned exports, and broader plotting/comparison responsibilities. The anticipated next sequence is the separately reviewed #22 correctness fix, then Magnus (#7).
