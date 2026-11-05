(function () {
    "use strict";
    const output = document.getElementById("results");
    const outcomes = [];
    function assert(condition, message) { if (!condition) throw new Error(message); }
    function test(name, action) {
        try { action(); outcomes.push({ name, passed: true }); }
        catch (error) { outcomes.push({ name, passed: false, message: error.message }); }
    }
    const result = (id, speed = 100) => ({ resultId: id, executedScenario: { launch: { speedMph: speed, mode: "contact" } }, points: [] });
    function ready() { const state = new LpbState.ExperimentState(); state.accept(state.begin("run"), result("first")); return state; }
    test("Pending edits, invalid fields, and reverting edits", () => {
        const state = ready();
        assert(!state.pending({ speedMph: Number("100.0"), mode: "contact" }), "Numeric formatting must not imply changes");
        assert(state.pending({ speedMph: 80, mode: "contact" }), "Changed speed must be pending");
        assert(state.pending({ speedMph: NaN, mode: "contact" }), "Incomplete input must be pending");
        assert(!state.pending({ speedMph: 100, mode: "contact" }), "Reverted input must match");
    });
    test("Display uses captured result despite edited form", () => {
        const state = ready(); const ticket = state.begin("display");
        assert(ticket.resultId === "first", "Display must capture active identity");
        assert(state.accept(ticket, result("first")), "Display response should apply");
        assert(state.pending({ speedMph: 80, mode: "contact" }), "Display must not clear physical pending changes");
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
        assert(state.pending({ speedMph: 90, mode: "contact" }), "Later form edit must remain pending");
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
    const failed = outcomes.filter(x => !x.passed).length;
    output.textContent = outcomes.map(x => `${x.passed ? "PASS" : "FAIL"} ${x.name}${x.message ? `: ${x.message}` : ""}`).join("\n") + `\n\n${outcomes.length - failed}/${outcomes.length} passed`;
    document.title = `${failed ? "FAIL" : "PASS"} · LPB state tests`;
    globalThis.lpbStateTestResults = { passed: outcomes.length - failed, failed, outcomes };
})();
