(function () {
    "use strict";
    const state = new LpbState.ExperimentState();
    const tabId = crypto.randomUUID();
    const form = document.querySelector("#launch-form");
    const density = document.querySelector("#display-count");
    const runButton = document.querySelector("#run");
    const displayButton = document.querySelector("#resample");
    const ft = 0.3048;
    let busy = false;
    const number = value => Number.isFinite(value)
        ? (value !== 0 && (Math.abs(value) < 0.0001 || Math.abs(value) >= 1e7) ? value.toExponential(5) : value.toLocaleString("en-US", { maximumFractionDigits: 5 }))
        : "Unavailable";
    function readLaunch() {
        return Object.fromEntries([...form.elements].filter(el => el.name).map(el => [el.name, el.type === "number" ? el.valueAsNumber : el.value]));
    }
    function pending() {
        const label = document.querySelector("#pending");
        const dirty = state.pending(readLaunch());
        label.textContent = state.active ? (dirty ? "Unrun input changes" : "Inputs match run") : "Not run";
        label.classList.toggle("pending", dirty);
        runButton.disabled = busy;
        displayButton.disabled = busy || !state.active || state.unavailable;
        density.disabled = busy;
    }
    function error(message, fields) {
        const box = document.querySelector("#errors");
        box.replaceChildren();
        box.hidden = !message;
        if (!message) return;
        box.append(document.createTextNode(message));
        if (fields) {
            const list = document.createElement("ul");
            for (const [name, text] of Object.entries(fields)) {
                const item = document.createElement("li"); item.textContent = text; list.append(item);
                form.elements.namedItem(name)?.setAttribute("aria-invalid", "true");
            }
            box.append(list);
        }
    }
    function metric(title, value, detail) {
        const el = document.createElement("div"); el.className = "metric";
        for (const [tag, text] of [["strong", value], ["span", title], ["small", detail]]) {
            const child = document.createElement(tag); child.textContent = text; el.append(child);
        }
        return el;
    }
    function renderContext() {
        const r = state.active;
        if (!r) return;
        const c = r.executedScenario.launch;
        document.querySelector("#run-label").textContent = state.failedRun || state.unavailable ? "Last successful run" : "Executed scenario";
        document.querySelector("#mode-label").textContent = c.mode === "contact" ? "Contact-aware flight" : "Unconstrained · ground contact disabled";
        document.querySelector("#captured").textContent = `${r.executedScenario.model} · ${number(c.speedMph)} mph · elevation ${number(c.elevationDegrees)}° · field azimuth ${number(c.fieldAzimuthDegrees)}° · initial center ${number(c.centerHeightFeet)} ft above ground · ground Z ${number(c.groundElevationFeet)} ft · radius ${number(c.ballRadiusInches)} in · gravity ${number(c.gravityMagnitude)} m/s² · dt ${number(c.timeStepSeconds)} s · requested ${number(c.durationSeconds)} s`;
        const m = r.measurements, maximum = r.landmarks.maximum;
        document.querySelector("#metrics").replaceChildren(
            metric(r.completionReason === "GroundContact" ? "Ground contact — ball center" : "Duration-limited terminal", `${number(r.actualElapsedSeconds)} s`, `Requested ${number(r.requestedDurationSeconds)} s`),
            metric("Maximum center height above ground", m.maximumHeightAboveGroundMetres === null ? "Unavailable" : `${number(m.maximumHeightAboveGroundMetres / ft)} ft`, maximum ? `At ${number(maximum.timeSeconds)} s · within simulated interval` : "Numerical maximum unavailable"),
            metric("Horizontal displacement magnitude", m.horizontalDistanceMetres === null ? "Unavailable" : `${number(m.horizontalDistanceMetres / ft)} ft`, "Final minus initial XY · not path length"),
            metric("Terminal field XYZ (ft)", `${number(r.landmarks.terminal.xMetres / ft)}, ${number(r.landmarks.terminal.yMetres / ft)}, ${number(r.landmarks.terminal.zMetres / ft)}`, r.landmarks.contact ? "Confirmed contact · center, not ground point" : "No confirmed ground contact")
        );
        const diag = document.querySelector("#diagnostics"); diag.replaceChildren();
        const counts = document.createElement("p"); counts.textContent = `Integration dt ${number(c.timeStepSeconds)} s · recorded ${r.originalSampleCount.toLocaleString()} · display requested ${r.requestedDisplayPointCount.toLocaleString()} / actual ${r.actualDisplayPointCount.toLocaleString()} · ${r.samplingSource}`; diag.append(counts);
        const reference = document.createElement("p"); reference.textContent = `3D uses field Z; side profile uses Z − ground elevation. Ground Z = ${number(c.groundElevationFeet)} ft; center-contact reference Z = ${number((r.executedScenario.groundElevationMetres + r.executedScenario.ballRadiusMetres) / ft)} ft (${number(r.executedScenario.ballRadiusMetres / ft)} ft above ground).`; diag.append(reference);
        for (const warning of r.warnings) { const p = document.createElement("p"); p.className = "notice"; p.textContent = warning; diag.append(p); }
        pending();
    }
    async function execute(kind) {
        if (busy) return;
        if ((kind === "run" && !form.reportValidity()) || !density.reportValidity()) return;
        const launch = readLaunch();
        const ticket = state.begin(kind);
        const resultId = ticket.resultId;
        busy = true; error(null); pending();
        for (const el of form.elements) el.removeAttribute("aria-invalid");
        document.querySelector("#operation").textContent = kind === "run" ? "Running the authoritative C# simulation…" : "Updating display points from the captured result…";
        try {
            const response = await fetch(kind === "run" ? "/api/simulations" : `/api/simulations/${resultId}/display`, {
                method: "POST", headers: { "Content-Type": "application/json", "X-Lpb-Tab-Id": tabId },
                body: JSON.stringify(kind === "run" ? { launch, displayPointCount: density.valueAsNumber } : { displayPointCount: density.valueAsNumber }), cache: "no-store"
            });
            const payload = await response.json();
            if (!response.ok) {
                if (state.fail(ticket, response.status)) { error(payload.message, payload.fields); renderContext(); }
                return;
            }
            if (!state.accept(ticket, payload)) return;
            renderContext();
            try { await renderPlots(payload, ticket); }
            catch (limitation) {
                if (state.current(ticket)) {
                    const p = document.createElement("p"); p.className = "notice";
                    p.textContent = `Visualization limitation: ${limitation.message}`;
                    document.querySelector("#diagnostics").append(p);
                }
            }
        } catch (failure) {
            if (state.fail(ticket, 0)) { error(`The operation could not complete: ${failure.message}`); renderContext(); }
        } finally {
            if (state.current(ticket)) { busy = false; document.querySelector("#operation").textContent = ""; pending(); }
        }
    }
    function bounds(values) {
        const min = Math.min(...values), max = Math.max(...values);
        const pad = Math.max((max - min) * 0.06, 0.5);
        const range = [min - pad, max + pad];
        if (!range.every(Number.isFinite)) throw new Error("Coordinates exceed a safe plotting range. The numerical result remains available above.");
        return range;
    }
    async function renderPlots(r, ticket) {
        document.querySelector("#plot-3d > .empty")?.remove();
        const s = r.executedScenario;
        const direction = s.direction;
        function convert(p) {
            const q = { t: p.timeSeconds, x: p.xMetres / ft, y: p.yMetres / ft, z: p.zMetres / ft,
                h: (p.zMetres - s.groundElevationMetres) / ft,
                d: ((p.xMetres - r.landmarks.launch.xMetres) * direction.x + (p.yMetres - r.landmarks.launch.yMetres) * direction.y) / ft };
            if (!Object.values(q).every(Number.isFinite)) throw new Error("Coordinates exceed the display-unit range. The authoritative result is retained.");
            return q;
        }
        let points, marks, xr, yr, zr, dr, hr;
        const ground = s.groundElevationMetres / ft, radius = s.ballRadiusMetres / ft;
        try {
            points = r.points.map(convert);
            marks = [["Launch", r.landmarks.launch, "#126e6b", "circle"],
                ["Maximum over simulated interval", r.landmarks.maximum, "#bd7d21", "diamond"],
                [r.landmarks.contact ? "Ground contact — ball center" : "Terminal — requested duration reached", r.landmarks.terminal, r.landmarks.contact ? "#182c38" : "#8a568b", "square"]]
                .filter(([, point]) => point).map(([name, point, color, symbol]) => ({ name, point: convert(point), color, symbol }));
            const all = points.concat(marks.map(m => m.point));
            xr = bounds([0, ...all.map(p => p.x)]); yr = bounds([0, ...all.map(p => p.y)]);
            zr = bounds([ground, ground + radius, ...all.map(p => p.z)]);
            dr = bounds([0, ...all.map(p => p.d)]); hr = bounds([0, radius, ...all.map(p => p.h)]);
        } catch (failure) {
            for (const id of ["plot-3d", "plot-side", "plot-top"]) { Plotly.purge(id); document.getElementById(id).replaceChildren(); }
            throw failure;
        }
        const pathColor = "#126e6b", dash = s.launch.mode === "unconstrained" ? "dash" : "solid";
        function trace(data, view, name, color, mode, symbol) {
            const three = view === "3d";
            const result = { type: three ? "scatter3d" : "scatter", name, mode, x: data.map(p => view === "side" ? p.d : p.x),
                y: data.map(p => view === "side" ? p.h : p.y), line: { color, width: three ? 4 : 2, dash },
                marker: { color, size: three ? 5 : 8, symbol: symbol || "circle" },
                customdata: data.map(p => [p.t, p.x, p.y, p.z, p.h]),
                hovertemplate: "t %{customdata[0]:.6g} s<br>X %{customdata[1]:.6g} ft · Y %{customdata[2]:.6g} ft<br>Z %{customdata[3]:.6g} ft (field)<br>Center %{customdata[4]:.6g} ft above ground<extra>%{fullData.name}</extra>" };
            if (three) result.z = data.map(p => p.z);
            return result;
        }
        function traces(view) {
            return [trace(points, view, s.launch.mode === "unconstrained" ? "Unconstrained trajectory" : "Trajectory", pathColor, points.length === 1 ? "markers" : "lines"),
                ...marks.map(m => trace([m.point], view, m.name, m.color, "markers", m.symbol))];
        }
        function plane(z, name, color) {
            return { type: "scatter3d", mode: "lines", name, x: [xr[0], xr[1], xr[1], xr[0], xr[0]], y: [yr[0], yr[0], yr[1], yr[1], yr[0]], z: Array(5).fill(z), line: { color, width: 1, dash: "dot" }, hoverinfo: "skip" };
        }
        const base = { font: { family: "-apple-system, BlinkMacSystemFont, Segoe UI, sans-serif", size: 10, color: "#536775" },
            paper_bgcolor: "#ffffff", plot_bgcolor: "#ffffff", margin: { l: 58, r: 20, t: 16, b: 55 },
            legend: { orientation: "h", y: -0.23, x: 0, font: { size: 9 } }, uirevision: r.resultId };
        const axis = (title, range) => ({ title: { text: title, font: { size: 10 } }, range, gridcolor: "#e9eeeb", zerolinecolor: "#b8c5bf", automargin: true });
        const three = { ...base, margin: { l: 0, r: 0, t: 0, b: 25 }, legend: { ...base.legend, y: 0 }, scene: {
            xaxis: axis("X → first base / right field (ft)", xr), yaxis: axis("Y → third base / left field (ft)", yr),
            zaxis: axis("Ball-center Z — field origin (ft)", zr), aspectmode: "data", camera: { eye: { x: 1.5, y: -1.8, z: 1.2 }, projection: { type: "orthographic" } } } };
        const side = { ...base, xaxis: axis("Downrange displacement from launch (ft)", dr), yaxis: { ...axis("Center height above local ground (ft)", hr), scaleanchor: "x", scaleratio: 1 }, shapes: [
            { type: "rect", xref: "paper", x0: 0, x1: 1, y0: Math.min(hr[0], 0), y1: 0, fillcolor: "#fbefec", line: { width: 0 }, layer: "below" },
            { type: "line", xref: "paper", x0: 0, x1: 1, y0: 0, y1: 0, line: { color: "#718278", dash: "dash" } },
            { type: "line", xref: "paper", x0: 0, x1: 1, y0: radius, y1: radius, line: { color: "#a9bbb1", dash: "dot" } }
        ], annotations: [{ xref: "paper", x: 0.98, y: 0, text: "Local ground", showarrow: false, yanchor: "top", font: { size: 9 } },
            { xref: "paper", x: 0.98, y: radius, text: "Center-contact reference", showarrow: false, yanchor: "bottom", font: { size: 9 } }] };
        const top = { ...base, xaxis: axis("X → first base / right field (ft)", xr), yaxis: { ...axis("Y → third base / left field (ft)", yr), scaleanchor: "x", scaleratio: 1 } };
        const rayLength = Math.max(1, Math.max(xr[1], yr[1]) * 0.85);
        const rays = [["+X / first-base direction", [0, rayLength], [0, 0]], ["+Y / third-base direction", [0, 0], [0, rayLength]], ["Ideal center field", [0, rayLength], [0, rayLength]]]
            .map(([name, x, y]) => ({ type: "scatter", mode: "lines", name, x, y, line: { color: "#c5cec7", width: 1, dash: "dot" }, hoverinfo: "skip", showlegend: false }));
        const config = { responsive: true, displaylogo: false, modeBarButtonsToRemove: ["toImage", "sendChartToCloud"], showLink: false, plotlyServerURL: "" };
        const results = await Promise.allSettled([
            Plotly.react("plot-3d", [...traces("3d"), plane(ground, "Local ground reference", "#718278"), plane(ground + radius, "Center-contact reference", "#a9bbb1")], three, config),
            Plotly.react("plot-side", traces("side"), side, config), Plotly.react("plot-top", [...rays, ...traces("top")], top, config)
        ]);
        if (!state.current(ticket)) return;
        results.forEach((result, i) => {
            if (result.status === "rejected") {
                const el = document.getElementById(["plot-3d", "plot-side", "plot-top"][i]); Plotly.purge(el);
                const p = document.createElement("p"); p.className = "empty"; p.textContent = `${i === 0 ? "3D" : "Projection"} unavailable in this browser. ${result.reason?.message || "Check WebGL support."}`; el.replaceChildren(p);
            }
        });
    }
    form.addEventListener("submit", event => { event.preventDefault(); execute("run"); });
    form.addEventListener("input", event => { event.target.removeAttribute("aria-invalid"); pending(); });
    displayButton.addEventListener("click", () => execute("display"));
    window.addEventListener("pagehide", () => {
        if (state.active) fetch(`/api/simulations/${state.active.resultId}`, { method: "DELETE", headers: { "Content-Type": "application/json", "X-Lpb-Tab-Id": tabId }, body: "{}", keepalive: true }).catch(() => {});
    });
    pending();
})();
