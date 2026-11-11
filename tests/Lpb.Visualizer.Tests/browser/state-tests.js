(function () {
    "use strict";
    const output = document.getElementById("results");
    const outcomes = [];
    function assert(condition, message) { if (!condition) throw new Error(message); }
    function test(name, action) {
        try { action(); outcomes.push({ name, passed: true }); }
        catch (error) { outcomes.push({ name, passed: false, message: error.message }); }
    }
    const launch = (edits = {}) => ({ physicalModel: "gravity-only", speedMph: 100, mode: "contact",
        elevationDegrees: 30, fieldAzimuthDegrees: 45, centerHeightFeet: 3, groundElevationFeet: 0,
        ballRadiusInches: 1.45, gravityMagnitude: 9.80665, timeStepSeconds: .01, durationSeconds: 10,
        ballMassGrams: 145, airDensity: 1.225, dragCoefficient: .35, ...edits });
    const result = (id, speed = 100, edits = {}) => ({ resultId: id,
        executedScenario: { launch: LpbState.applicableLaunch(launch({ speedMph: speed, ...edits })) }, points: [] });
    function ready() { const state = new LpbState.ExperimentState(); state.accept(state.begin("run"), result("first")); return state; }
    test("Pending edits, invalid fields, and reverting edits", () => {
        const state = ready();
        assert(!state.pending(launch({ speedMph: Number("100.0") })), "Numeric formatting must not imply changes");
        assert(state.pending(launch({ speedMph: 80 })), "Changed speed must be pending");
        assert(state.pending(launch({ speedMph: NaN })), "Incomplete input must be pending");
        assert(!state.pending(launch()), "Reverted input must match");
    });
    test("Display uses captured result despite edited form", () => {
        const state = ready(); const ticket = state.begin("display");
        assert(ticket.resultId === "first", "Display must capture active identity");
        assert(state.accept(ticket, result("first")), "Display response should apply");
        assert(state.pending(launch({ speedMph: 80 })), "Display must not clear physical pending changes");
        assert(state.active.executedScenario.launch.speedMph === 100, "Captured launch must remain unchanged");
    });
    test("Failed new run preserves previous result", () => {
        const state = ready(); const previous = state.active;
        assert(state.fail(state.begin("run"), 422), "Current failure should be accepted");
        assert(state.active === previous && state.failedRun, "Previous result must be labeled last successful");
        state.accept(state.begin("display"), result("first"));
        assert(state.failedRun, "Display-only success must not erase the failed-run context");
    });
    test("Edits made during a run remain pending after success", () => {
        const state = ready(); const ticket = state.begin("run");
        assert(state.accept(ticket, result("second", 80)), "Latest run must apply");
        assert(state.pending(launch({ speedMph: 90 })), "Later form edit must remain pending");
    });
    test("Out-of-order run responses cannot replace newer result", () => {
        const state = ready(); const older = state.begin("run"); const newer = state.begin("run");
        assert(state.accept(newer, result("newer")), "New run should apply");
        assert(!state.accept(older, result("older")), "Old response must be ignored");
        assert(!state.fail(older, 422), "Old failure must be ignored");
        assert(state.active.resultId === "newer" && !state.failedRun, "New result must remain active");
    });
    test("Old display response or error cannot affect a new result", () => {
        const state = ready(); const display = state.begin("display");
        state.accept(state.begin("run"), result("second", 80));
        assert(!state.accept(display, result("first")), "Old display must be ignored");
        assert(!state.fail(display, 404), "Old resource error must not invalidate new result");
        assert(!state.current(display), "Old loading-state update must be ignored");
        assert(state.active.resultId === "second" && !state.unavailable, "New result must remain available");
    });
    test("Wrong result ID is rejected even for current display request", () => {
        const state = ready(); assert(!state.accept(state.begin("display"), result("wrong")), "Mismatched result must not apply");
        assert(state.active.resultId === "first", "Keep active result");
    });
    test("Missing cache result preserves display and requires explicit rerun", () => {
        const state = ready(); state.fail(state.begin("display"), 404);
        assert(state.unavailable && state.active.resultId === "first", "Keep last visible experiment without rerunning");
        state.accept(state.begin("run"), result("second")); assert(!state.unavailable, "Explicit successful run restores availability");
    });
    test("Inactive invalid aerodynamics are excluded from requests and pending changes", () => {
        const state = ready();
        const values = launch({ ballMassGrams: NaN, airDensity: -1, dragCoefficient: Infinity });
        assert(!state.pending(values), "Inactive aerodynamic edits must not be pending");
        const payload = LpbState.applicableLaunch(values);
        for (const key of ["ballMassGrams", "airDensity", "dragCoefficient"])
            assert(!Object.hasOwn(payload, key), "Inactive field must be absent from request");
        assert(!JSON.stringify(payload).includes("null"), "Inactive NaN must not become JSON null");
    });
    test("Model switching and reverting compare applicable settings", () => {
        const state = ready();
        assert(state.pending(launch({ physicalModel: "gravity-drag" })), "Model change must be pending");
        assert(!state.pending(launch({ ballMassGrams: 200 })), "Returning to gravity ignores retained aerodynamic changes");
        state.accept(state.begin("run"), result("drag", 100, { physicalModel: "gravity-drag" }));
        assert(state.pending(launch({ physicalModel: "gravity-only" })), "Switching away from executed drag is pending");
        assert(!state.pending(launch({ physicalModel: "gravity-drag" })), "Returning to original model/settings clears pending");
        for (const key of ["ballMassGrams", "airDensity", "dragCoefficient"])
            assert(state.pending(launch({ physicalModel: "gravity-drag", [key]: NaN })), "Active incomplete field must be pending");
        assert(state.pending(launch({ physicalModel: "gravity-drag", ballMassGrams: 200 })), "Active mass change must be pending");
        assert(!state.pending(launch({ physicalModel: "gravity-drag", ballMassGrams: Number("145.0") })), "Reverting aerodynamic value clears pending");
    });
    test("Drag display keeps captured context after model or invalid aerodynamic edits", () => {
        const state = ready();
        const captured = result("drag", 100, { physicalModel: "gravity-drag", airDensity: 0 });
        state.accept(state.begin("run"), captured);
        const display = state.begin("display");
        assert(display.resultId === "drag", "Density change must target captured drag result");
        assert(state.pending(launch()), "Pending gravity selection must not alter captured drag");
        assert(state.accept(display, captured), "Captured display response applies");
        assert(state.active.executedScenario.launch.airDensity === 0, "Disabled-drag setting stays captured");
        assert(state.pending(launch({ physicalModel: "gravity-drag", ballMassGrams: NaN })), "Invalid pending aero edit stays pending after display");
        state.fail(state.begin("run"), 400);
        assert(state.active === captured && state.failedRun, "Invalid new drag run must preserve previous result");
    });
    const failed = outcomes.filter(x => !x.passed).length;
    output.textContent = outcomes.map(x => `${x.passed ? "PASS" : "FAIL"} ${x.name}${x.message ? `: ${x.message}` : ""}`).join("\n") + `\n\n${outcomes.length - failed}/${outcomes.length} passed`;
    document.title = `${failed ? "FAIL" : "PASS"} · LPB state tests`;
    globalThis.lpbStateTestResults = { passed: outcomes.length - failed, failed, outcomes };
})();
