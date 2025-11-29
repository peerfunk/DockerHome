// =============================================================
// API ENDPOINTS
// =============================================================
const api = {
    all: "/api/containers/all",
    display: "/api/containers/display"
};

// =============================================================
// ELEMENT BUILDER
// =============================================================
function el(tag, attrs = {}, ...children) {
    const e = document.createElement(tag);

    for (const k in attrs) {
        if (k === "on") {
            Object.entries(attrs[k]).forEach(([ev, fn]) =>
                e.addEventListener(ev, fn)
            );
        }
        else if (k === "html") {
            e.innerHTML = attrs[k];
        }
        else if (k === "value" && (tag === "input" || tag === "textarea")) {
            e.value = attrs[k];
        }
        else {
            if (attrs[k] !== undefined && attrs[k] !== null)
                e.setAttribute(k, attrs[k]);
        }
    }

    children.forEach(c => {
        if (c == null) return;
        if (typeof c === "string") e.appendChild(document.createTextNode(c));
        else e.appendChild(c);
    });

    return e;
}

// =============================================================
// NETWORK HELPERS
// =============================================================
async function fetchJson(url) {
    const res = await fetch(url);
    if (!res.ok) throw new Error(await res.text());
    return res.json();
}

async function postJson(url, data) {
    const res = await fetch(url, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(data)
    });
    if (!res.ok) throw new Error(await res.text());
}

// =============================================================
// UI HELPERS
// =============================================================
function statusPill(isRunning) {
    const cls = isRunning ? "status-pill status-running"
        : "status-pill status-stopped";
    return el("span", { class: cls }, isRunning ? "Running" : "Stopped");
}
function checkbox(attrs = {}) {
    const isChecked = attrs.checked === true;
    delete attrs.checked;

    const input = el("input", {
        type: "checkbox",
        ...(isChecked ? { checked: true } : {}),
        ...attrs
    });

    return input;
}

function portSummary(ports) {
    if (!ports || ports.length === 0) return "No exposed ports";
    return ports.join(", ");
}

function groupByProject(list) {
    const groups = {};
    list.forEach(c => {
        const g = c.composeProject || "uncategorized";
        if (!groups[g]) groups[g] = [];
        groups[g].push(c);
    });
    return groups;
}

// =============================================================
// MAIN APPLICATION
// =============================================================
async function main() {
    const root = document.getElementById("app");

    let containers = [];
    let config = [];

    // ---------------------------------------------
    // LOAD CONFIG ONLY
    // ---------------------------------------------
    async function loadConfigOnly() {
        try {
            config = await fetchJson(api.display);
        } catch {
            config = [];
        }
    }
   

    // ---------------------------------------------
    // LOAD ALL + CONFIG FOR EDIT VIEW
    // ---------------------------------------------
    async function loadAllForEdit() {
        containers = await fetchJson(api.all);

        try {
            config = await fetchJson(api.display);
        } catch {
            config = [];
        }

        const map = new Map(config.map(c => [c.id, c]));

        containers.forEach(c => {
            let cfg = map.get(c.id);

            if (!cfg) {
                cfg = {
                    id: c.id,
                    name: c.name,
                    description: c.description || "",
                    iconUrl: c.iconUrl || "",
                    selected: c.urls?.length > 0,
                    urls: c.urls || [],
                    composeProject: c.composeProject || ""
                };
                config.push(cfg);
            } else {
                cfg.iconUrl ||= c.iconUrl;
                cfg.description ||= c.description;
                cfg.composeProject ||= c.composeProject;
                cfg.name ||= c.name;
                cfg.urls ||= c.urls || [];
            }
        });
    }

    // =============================================================
    // NAVIGATION
    // =============================================================
    function renderNav() {
        return el(
            "div",
            { class: "nav" },
            el("button", { class: "btn", on: { click: renderOverview } }, "Overview"),
            el("button", { class: "btn", on: { click: renderEdit } }, "Edit View")
        );
    }

    // =============================================================
    // OVERVIEW PAGE
    // =============================================================
    async function renderOverview() {
        root.innerHTML = "";
        await loadConfigOnly();

        const groups = {};
        for (const cfg of config.filter(x => x.selected)) {
            const group = cfg.composeProject || "uncategorized";
            if (!groups[group]) groups[group] = [];
            groups[group].push(cfg);
        }

        const wrapper = el("div", { class: "overview-container" });

        for (const [project, list] of Object.entries(groups)) {
            wrapper.appendChild(el("h2", {}, project));

            const grid = el("div", { class: "grid" });

            for (const cfg of list) {
                const hasUrl = cfg.urls?.length > 0;

                const card = el(
                    "div",
                    {
                        class: "card" + (cfg.urls?.length ===1 ? " clickable" : ""),
                        on: { click: () => (cfg.urls?.length ===1) && window.open(cfg.urls[0], "_blank") }
                    }
                );

                // ICON
                card.appendChild(
                    el(
                        "div",
                        { class: "icon-area" },
                        cfg.iconUrl
                            ? el("img", {
                                src: cfg.iconUrl,
                                class: "icon-img",
                                on: {
                                    error: e => {
                                        const ph = el(
                                            "div",
                                            { class: "icon-placeholder" },
                                            cfg.name?.[0]?.toUpperCase() ?? "?"
                                        );
                                        e.target.replaceWith(ph);
                                    }
                                }
                            })
                            : el(
                                "div",
                                { class: "icon-placeholder" },
                                cfg.name?.[0]?.toUpperCase() ?? "?"
                            )
                    )
                );

                card.appendChild(
                    el("div", { class: "title-row" },
                        el("h3", {}, cfg.name),
                        statusPill(cfg.running)
                    )
                );

                card.appendChild(
                    el("p", { class: "description" }, cfg.description || cfg.image)
                );

                if (hasUrl) {
                    const urlElements = [
                        el("span", {}, "URLs: ")
                    ];

                    if (Array.isArray(cfg.urls)) {
                        cfg.urls.forEach(x => {
                            urlElements.push(
                                el("a", { href: x, target: "_blank", class: "url-link" }, x)
                            );
                        });
                    }

                    // NOTE the spread operator (...) so children are passed as individual args
                    card.appendChild(
                        el("div", { class: "urls" }, ...urlElements)
                    );
                }


                card.appendChild(
                    el("div", { class: "ports" }, "Ports: " + portSummary(cfg.ports))
                );

                grid.appendChild(card);
            }

            wrapper.appendChild(grid);
        }

        root.appendChild(renderNav());
        root.appendChild(wrapper);
    }

    // =============================================================
    // EDIT PAGE
    // =============================================================
    async function renderEdit() {
        root.innerHTML = "";

        await loadAllForEdit();
        const groups = groupByProject(containers);

        const wrapper = el("div", { class: "edit-container" });

        // ---------------------------------------------------------
        // SELECT ALL / DESELECT ALL
        // ---------------------------------------------------------
        wrapper.appendChild(
            el("div", { class: "row" },

                el("button", {
                    class: "btn",
                    on: {
                        click: () => {
                            config.forEach(cfg => (cfg.selected = true));
                            wrapper.querySelectorAll(".edit-card input[type=checkbox]")
                                .forEach(cb => (cb.checked = true));
                        }
                    }
                }, "Select All"),

                el("button", {
                    class: "btn",
                    on: {
                        click: () => {
                            config.forEach(cfg => (cfg.selected = false));
                            wrapper.querySelectorAll(".edit-card input[type=checkbox]")
                                .forEach(cb => (cb.checked = false));
                        }
                    }
                }, "Deselect All")
            )
        );

        // ---------------------------------------------------------
        // EDIT CARDS BY GROUP
        // ---------------------------------------------------------
        for (const [project, list] of Object.entries(groups)) {
            wrapper.appendChild(el("h2", {}, project));

            for (const c of list) {
                const cfg = config.find(x => x.id === c.id);
                const card = el("div", { class: "edit-card" });

                // CHECKBOX
                const checkboxProps = {
                    type: "checkbox",
                    checked: cfg?.selected === true ? true : undefined,
                    on: { change: e => (cfg.selected = e.target.checked) }
                };
                if (!cfg?.selected) delete checkboxProps.checked;
                
                card.appendChild(
                    el("label", { class: "row" },
                        el("input", checkboxProps),
                        el("strong", {}, cfg.name),
                        el("span", { style: "margin-left:auto" }, statusPill(c.running))
                    )
                );

                // ICON BLOCK
                let preview;
                const iconArea = el("div", {
                    id: "iconarea",
                    class: "icon-area nice-icon-block"
                });

                const iconInput = el("input", {
                    type: "text",
                    class: "icon-url-input",
                    placeholder: "Icon URL",
                    value: cfg.iconUrl
                });

                function buildPreview(url) {
                    if (!url)
                        return el(
                            "div",
                            { class: "icon-placeholder" },
                            (cfg.name || c.name)?.[0]?.toUpperCase() ?? "?"
                        );

                    const img = el("img", { src: url, class: "icon-preview" });

                    img.addEventListener("error", () => {
                        if (preview?.parentNode) preview.remove();
                        preview = el(
                            "div",
                            { class: "icon-placeholder" },
                            (cfg.name || c.name)?.[0]?.toUpperCase() ?? "?"
                        );
                        iconArea.appendChild(preview);
                    });

                    return img;
                }

                preview = buildPreview(cfg.iconUrl || c.iconUrl);

                iconInput.addEventListener("input", e => {
                    cfg.iconUrl = e.target.value;
                    if (preview?.parentNode) preview.remove();
                    preview = buildPreview(cfg.iconUrl);
                    iconArea.appendChild(preview);
                });

                iconArea.appendChild(iconInput);
                iconArea.appendChild(preview);
                card.appendChild(iconArea);

                // NAME
                card.appendChild(
                    el("input", {
                        type: "text",
                        placeholder: "Custom Name",
                        value: cfg.name || c.name,
                        on: { input: e => (cfg.name = e.target.value) }
                    })
                );

                // DESCRIPTION
                card.appendChild(
                    el("input", {
                        type: "text",
                        placeholder: "Description",
                        value: cfg.description,
                        on: { input: e => (cfg.description = e.target.value) }
                    })
                );

                // URLS
                card.appendChild(
                    el("textarea", {
                        placeholder: "URLs (one per line)",
                        value: (cfg.urls || []).join("\n"),
                        on: {
                            input: e => {
                                cfg.urls = e.target.value
                                    .split("\n")
                                    .map(x => x.trim())
                                    .filter(x => x.length > 0);
                            }
                        }
                    })
                );

                // CATEGORY
                card.appendChild(
                    el("input", {
                        type: "text",
                        placeholder: "Category",
                        value: cfg.composeProject ,
                        on: { input: e => (cfg.composeProject = e.target.value) }
                    })
                );

                // PORTS
                card.appendChild(
                    el("div", { class: "row" }, "Ports: " + portSummary(c.ports))
                );

                wrapper.appendChild(card);
            }
        }

        // SAVE BUTTON
        wrapper.appendChild(
            el(
                "button",
                {
                    class: "btn primary",
                    on: {
                        click: async () => {
                            await postJson(api.display, config);
                            alert("Saved!");
                        }
                    }
                },
                "Save Configuration"
            )
        );

        root.appendChild(renderNav());
        root.appendChild(wrapper);
    }

    // DEFAULT LOAD
    await loadConfigOnly();
    renderOverview();
}

main();
