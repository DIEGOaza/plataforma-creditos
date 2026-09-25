(function () {
    "use strict";

    if (window.__solicitudesHubIniciado) {
        return;
    }

    var statusElements = Array.from(
        document.querySelectorAll("[data-solicitudes-connection-status]"));
    var liveElements = Array.from(
        document.querySelectorAll("[data-solicitudes-live-status]"));

    if (statusElements.length === 0) {
        return;
    }

    window.__solicitudesHubIniciado = true;

    function setConnectionStatus(message, cssClass) {
        statusElements.forEach(function (element) {
            element.textContent = message;
            element.className = "badge " + cssClass;
        });
    }

    function setLiveMessage(message, kind) {
        liveElements.forEach(function (element) {
            element.textContent = message;
            element.classList.remove("d-none", "alert-success", "alert-info", "alert-danger");
            element.classList.add("alert-" + kind);
        });
    }

    function readProperty(payload, camelName, pascalName) {
        if (!payload) {
            return undefined;
        }

        return payload[camelName] !== undefined
            ? payload[camelName]
            : payload[pascalName];
    }

    function normalizeNotification(payload) {
        var rawState = readProperty(payload, "estado", "Estado");
        var stateByValue = {
            0: "Pendiente",
            1: "Aprobado",
            2: "Rechazado"
        };
        var state = typeof rawState === "number"
            ? stateByValue[rawState] || String(rawState)
            : String(rawState || "");

        return {
            id: String(readProperty(payload, "solicitudId", "SolicitudId") || ""),
            state: state,
            reason: readProperty(payload, "motivoRechazo", "MotivoRechazo") || "",
            amount: readProperty(payload, "montoSolicitado", "MontoSolicitado")
        };
    }

    function updateStatusElement(element, state) {
        if (!element) {
            return;
        }

        var labels = {
            Pendiente: "Pendiente",
            Aprobado: "Aprobada",
            Rechazado: "Rechazada"
        };
        var classes = {
            Pendiente: "bg-warning text-dark",
            Aprobado: "bg-success",
            Rechazado: "bg-danger"
        };

        element.textContent = labels[state] || state;
        element.className = "badge " + (classes[state] || "bg-secondary");
    }

    function updateRequest(notification) {
        if (!notification.id) {
            return;
        }

        document.querySelectorAll("[data-solicitud-id]").forEach(function (row) {
            if (row.getAttribute("data-solicitud-id") !== notification.id) {
                return;
            }

            updateStatusElement(
                row.querySelector("[data-solicitud-estado]"),
                notification.state);
            var reason = row.querySelector("[data-solicitud-motivo]");
            if (reason) {
                reason.textContent = notification.reason || "—";
            }

        });

        document.querySelectorAll("[data-solicitud-detalle-id]").forEach(function (detail) {
            if (detail.getAttribute("data-solicitud-detalle-id") !== notification.id) {
                return;
            }

            updateStatusElement(
                detail.querySelector("[data-solicitud-estado]"),
                notification.state);
            var reason = detail.querySelector("[data-solicitud-motivo]");
            if (reason) {
                reason.textContent = notification.reason || "No aplica";
            }
        });
    }

    if (typeof window.signalR === "undefined") {
        setConnectionStatus("SignalR no disponible", "bg-danger");
        return;
    }

    var connection = new window.signalR.HubConnectionBuilder()
        .withUrl("/hubs/solicitudes")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(window.signalR.LogLevel.Warning)
        .build();
    var stopping = false;

    connection.on("SolicitudEstadoActualizado", function (payload) {
        var notification = normalizeNotification(payload);
        updateRequest(notification);
        document.dispatchEvent(new CustomEvent(
            "solicitudEstadoActualizado",
            { detail: notification }));
        var estadoTexto = notification.state === "Aprobado"
            ? "Aprobada"
            : notification.state === "Rechazado"
                ? "Rechazada"
                : notification.state;
        setLiveMessage(
            "La solicitud #" + notification.id + " fue actualizada a " +
                estadoTexto + ".",
            "success");
    });

    connection.onreconnecting(function () {
        setConnectionStatus("Reconectando...", "bg-warning text-dark");
    });

    connection.onreconnected(function () {
        setConnectionStatus("Conectado", "bg-success");
    });

    connection.onclose(function () {
        if (!stopping) {
            setConnectionStatus("Desconectado; reintentando...", "bg-danger");
            window.setTimeout(startConnection, 5000);
        }
    });

    async function startConnection() {
        if (stopping ||
            connection.state !== window.signalR.HubConnectionState.Disconnected) {
            return;
        }

        try {
            await connection.start();
            setConnectionStatus("Conectado", "bg-success");
        } catch (error) {
            setConnectionStatus("Sin conexión; reintentando...", "bg-danger");
            window.setTimeout(startConnection, 5000);
        }
    }

    window.addEventListener("beforeunload", function () {
        stopping = true;
        connection.stop();
    });

    startConnection();
})();
