(function (root) {
    "use strict";
    const commonFields = ["physicalModel", "mode", "speedMph", "elevationDegrees", "fieldAzimuthDegrees",
        "centerHeightFeet", "groundElevationFeet", "ballRadiusInches", "gravityMagnitude", "timeStepSeconds", "durationSeconds"];
    const aerodynamicFields = ["ballMassGrams", "airDensity", "dragCoefficient"];
    function launchFields(model) {
        return model === "gravity-drag" ? commonFields.concat(aerodynamicFields) : commonFields;
    }
    function applicableLaunch(values) {
        return Object.fromEntries(launchFields(values.physicalModel).map(key => [key, values[key]]));
    }
    class ExperimentState {
        constructor() {
            this.sequence = 0;
            this.active = null;
            this.failedRun = false;
            this.unavailable = false;
        }
        begin(kind) {
            return { sequence: ++this.sequence, kind, resultId: this.active?.resultId ?? null };
        }
        current(ticket) {
            return ticket.sequence === this.sequence &&
                (ticket.kind !== "display" || ticket.resultId === this.active?.resultId);
        }
        accept(ticket, response) {
            if (!this.current(ticket) || (ticket.kind === "display" && response.resultId !== ticket.resultId)) return false;
            this.active = response;
            if (ticket.kind === "run") this.failedRun = false;
            this.unavailable = false;
            return true;
        }
        fail(ticket, status) {
            if (!this.current(ticket)) return false;
            if (ticket.kind === "run") this.failedRun = this.active !== null;
            if (ticket.kind === "display" && status === 404) this.unavailable = true;
            return true;
        }
        pending(form) {
            if (!this.active) return false;
            const captured = this.active.executedScenario.launch;
            return launchFields(captured.physicalModel).some(key =>
                typeof captured[key] === "number"
                    ? !Number.isFinite(form[key]) || form[key] !== captured[key]
                    : form[key] !== captured[key]);
        }
    }
    root.LpbState = { ExperimentState, launchFields, applicableLaunch };
})(globalThis);
