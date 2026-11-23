const form = document.querySelector("#purchase-form");
const list = document.querySelector("#request-list");
const message = document.querySelector("#message");
const refresh = document.querySelector("#refresh");
const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
let busy = false;

function notify(text, error = false) {
    message.textContent = text;
    message.classList.toggle("error", error);
    message.setAttribute("role", error ? "alert" : "status");
}

async function request(url, options) {
    const response = await fetch(url, options);
    const text = await response.text();
    let data;
    try {
        data = JSON.parse(text);
    } catch {
        throw new Error(`The server returned an unexpected response (HTTP ${response.status}). Check the server logs.`);
    }
    if (!response.ok) throw new Error(data.error || data.detail || `Request failed (HTTP ${response.status}).`);
    return data;
}

function element(tag, text, className) {
    const node = document.createElement(tag);
    node.textContent = text;
    if (className) node.className = className;
    return node;
}

function setBusy(value) {
    busy = value;
    document.querySelectorAll("button").forEach(button => button.disabled = value);
}

async function loadRequests() {
    list.setAttribute("aria-busy", "true");
    try {
        const rows = await request("/demo/purchases/");
        document.querySelector("#total").textContent = rows.length;
        document.querySelector("#pending").textContent = rows.filter(row => row.details.decision === "PendingApproval" && row.status === "Running").length;
        document.querySelector("#approved").textContent = rows.filter(row => ["Approved", "AutoApproved"].includes(row.details.decision)).length;
        document.querySelector("#rejected").textContent = rows.filter(row => row.details.decision === "Rejected").length;
        list.replaceChildren();
        if (!rows.length) list.append(element("p", "No purchase requests yet. Submit your first equipment request.", "muted"));
        for (const row of rows) {
            const details = row.details;
            const card = element("article", "", "request");
            const top = element("div", "", "request-top");
            top.append(element("h3", details.equipment));
            const state = row.subStatus === "Faulted" ? "Faulted" : details.decision;
            top.append(element("span", state === "PendingApproval" ? "Awaiting manager" : state === "AutoApproved" ? "Auto-approved" : state, `badge ${state}`));
            card.append(top, element("p", `${details.site} / ${details.requestedBy}`, "muted"));
            card.append(element("div", money.format(details.estimatedCost), "amount"));
            card.append(element("p", details.justification));
            card.append(element("p", `Elsa: ${row.status} / ${row.subStatus} | ${new Date(row.createdAt).toLocaleString()}`, "meta"));
            card.append(element("p", `Workflow ID: ${row.workflowInstanceId}`, "meta"));
            if (row.canDecide) {
                const actions = element("div", "", "actions");
                for (const decision of ["approve", "reject"]) {
                    const button = element("button", decision === "approve" ? "Approve purchase" : "Reject purchase", decision);
                    button.type = "button";
                    button.disabled = busy;
                    button.addEventListener("click", () => decide(row.workflowInstanceId, decision));
                    actions.append(button);
                }
                card.append(actions);
            }
            list.append(card);
        }
    } finally {
        list.setAttribute("aria-busy", "false");
    }
}

form.addEventListener("submit", async event => {
    event.preventDefault();
    if (busy) return;
    const values = Object.fromEntries(new FormData(form));
    for (const key of Object.keys(values)) values[key] = values[key].trim();
    values.estimatedCost = Number(values.estimatedCost);
    setBusy(true);
    try {
        const result = await request("/api/workflows/purchases", {
            method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(values)
        });
        form.reset();
        notify(`Request ${result.workflowInstanceId} submitted. ${result.status === "AutoApproved" ? "Auto-approved by Elsa." : "Sent for manager review."}`);
        await loadRequests();
    } catch (error) {
        notify(error.message, true);
    } finally {
        setBusy(false);
    }
});

async function decide(id, decision) {
    if (busy || !window.confirm(`${decision === "approve" ? "Approve" : "Reject"} this demo purchase?`)) return;
    setBusy(true);
    try {
        const result = await request(`/demo/purchases/${encodeURIComponent(id)}/${decision}`, { method: "POST" });
        notify(result.message);
        await loadRequests();
    } catch (error) {
        notify(error.message, true);
    } finally {
        setBusy(false);
    }
}

refresh.addEventListener("click", async () => {
    if (busy) return;
    setBusy(true);
    try {
        await loadRequests();
        notify("Workflow statuses refreshed.");
    } catch (error) {
        notify(error.message, true);
    } finally {
        setBusy(false);
    }
});

loadRequests().catch(error => {
    list.replaceChildren(element("p", "Requests could not be loaded. Use Refresh to retry.", "muted"));
    notify(error.message, true);
});
