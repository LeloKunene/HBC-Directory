// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {
  document
    .querySelectorAll(".alert-success.alert-dismissible")
    .forEach(function (alertEl) {
      setTimeout(function () {
        const alert = bootstrap.Alert.getOrCreateInstance(alertEl);
        alert.close();
      }, 3000);
    });
});

/* Global toast notifications
  Renders into #app-toast-stack (see _Layout.cshtml) using the
  .app-toast / .app-toast-stack styles in css/site.css. Call from
  anywhere: window.showToast("Message", "success" | "error" | "info").*/
(function () {
  const ICONS = {
    success: "fa-solid fa-circle-check",
    error: "fa-solid fa-triangle-exclamation",
    info: "fa-solid fa-circle-info"
  };
  const AUTO_DISMISS_MS = 5000;
  const LEAVE_TRANSITION_MS = 250; // must match the CSS transition duration

  window.showToast = function (message, type) {
    if (!message) return;
    type = (type === "success" || type === "error") ? type : "info";

    const stack = document.getElementById("app-toast-stack");
    if (!stack) return;

    const toast = document.createElement("div");
    toast.className = "app-toast app-toast--" + type;
    toast.setAttribute("role", "status");

    const icon = document.createElement("i");
    icon.className = "app-toast-icon " + ICONS[type];

    const text = document.createElement("div");
    text.className = "app-toast-message";
    text.textContent = message; // textContent, not innerHTML — message may be user-entered data

    const close = document.createElement("button");
    close.type = "button";
    close.className = "app-toast-close";
    close.setAttribute("aria-label", "Dismiss");
    close.innerHTML = "&times;";

    toast.append(icon, text, close);
    stack.appendChild(toast);

    // Force a reflow so the browser registers the starting state before we
    // add --visible — otherwise the opacity/transform transition is skipped
    // and the toast just appears already at its end state.
    void toast.offsetWidth;
    toast.classList.add("app-toast--visible");

    let dismissed = false;
    function dismiss() {
      if (dismissed) return;
      dismissed = true;
      toast.classList.remove("app-toast--visible");
      toast.classList.add("app-toast--leaving");
      setTimeout(function () { toast.remove(); }, LEAVE_TRANSITION_MS);
    }

    close.addEventListener("click", dismiss);
    setTimeout(dismiss, AUTO_DISMISS_MS);
  };
})();

(function () {
  let overlay = null;
  let imgEl = null;
  let lastFocused = null;

  function build() {
    overlay = document.createElement("div");
    overlay.className = "app-lightbox";
    overlay.setAttribute("role", "dialog");
    overlay.setAttribute("aria-modal", "true");
    overlay.setAttribute("aria-label", "Photo viewer");

    imgEl = document.createElement("img");
    imgEl.className = "app-lightbox-img";

    const close = document.createElement("button");
    close.type = "button";
    close.className = "app-lightbox-close";
    close.setAttribute("aria-label", "Close");
    close.innerHTML = "&times;";
    close.addEventListener("click", closeLightbox);

    overlay.append(imgEl, close);
    document.body.appendChild(overlay);

    // Click the dark background (not the photo itself) to dismiss.
    overlay.addEventListener("click", function (e) {
      if (e.target === overlay) closeLightbox();
    });
  }

  function closeLightbox() {
    if (!overlay) return;
    overlay.classList.remove("app-lightbox--visible");
    setTimeout(function () {
      overlay.classList.remove("app-lightbox--open");
      if (lastFocused) { lastFocused.focus(); lastFocused = null; }
    }, 200); // matches the CSS transition duration
  }

  window.openLightbox = function (src, alt) {
    if (!src) return;
    if (!overlay) build();

    lastFocused = document.activeElement;
    imgEl.src = src;
    imgEl.alt = alt || "";

    overlay.classList.add("app-lightbox--open");
    void overlay.offsetWidth; // force reflow so the transition actually plays
    overlay.classList.add("app-lightbox--visible");
  };

  window.closeLightbox = closeLightbox;

  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && overlay && overlay.classList.contains("app-lightbox--visible")) {
      closeLightbox();
    }
  });
})();

(function () {
  window.PHONE_COUNTRY_CODES = [
    { code: "+27",  name: "South Africa" },
    { code: "+260", name: "Zambia" },
    { code: "+263", name: "Zimbabwe" },
    { code: "+267", name: "Botswana" },
    { code: "+258", name: "Mozambique" },
    { code: "+266", name: "Lesotho" },
    { code: "+268", name: "Eswatini" },
    { code: "+264", name: "Namibia" },
    { code: "+265", name: "Malawi" },
    { code: "+254", name: "Kenya" },
    { code: "+255", name: "Tanzania" },
    { code: "+256", name: "Uganda" },
    { code: "+234", name: "Nigeria" },
    { code: "+233", name: "Ghana" },
    { code: "+20",  name: "Egypt" },
    { code: "+212", name: "Morocco" },
    { code: "+44",  name: "United Kingdom" },
    { code: "+1",   name: "US / Canada" },
    { code: "+61",  name: "Australia" },
    { code: "+64",  name: "New Zealand" },
    { code: "+91",  name: "India" },
    { code: "+86",  name: "China" },
    { code: "+81",  name: "Japan" },
    { code: "+82",  name: "South Korea" },
    { code: "+49",  name: "Germany" },
    { code: "+33",  name: "France" },
    { code: "+39",  name: "Italy" },
    { code: "+34",  name: "Spain" },
    { code: "+31",  name: "Netherlands" },
    { code: "+32",  name: "Belgium" },
    { code: "+41",  name: "Switzerland" },
    { code: "+46",  name: "Sweden" },
    { code: "+47",  name: "Norway" },
    { code: "+45",  name: "Denmark" },
    { code: "+353", name: "Ireland" },
    { code: "+351", name: "Portugal" },
    { code: "+7",   name: "Russia" },
    { code: "+55",  name: "Brazil" },
    { code: "+52",  name: "Mexico" },
    { code: "+971", name: "UAE" },
    { code: "+966", name: "Saudi Arabia" },
    { code: "+974", name: "Qatar" },
    { code: "+65",  name: "Singapore" },
    { code: "+60",  name: "Malaysia" },
    { code: "+63",  name: "Philippines" },
    { code: "+92",  name: "Pakistan" }
  ];

  window.populatePhoneCodeSelects = function () {
    document.querySelectorAll("select.phone-country-code").forEach(function (sel) {
      if (sel.dataset.populated) return;
      const def = sel.dataset.default || "+27";
      window.PHONE_COUNTRY_CODES.forEach(function (c) {
        const opt = document.createElement("option");
        opt.value = c.code;
        opt.textContent = c.code + " " + c.name;
        if (c.code === def) opt.selected = true;
        sel.appendChild(opt);
      });
      sel.dataset.populated = "true";
    });
  };

  // Joins the code + local-number inputs into the hidden field that
  // actually gets submitted. Wire this to a form's onsubmit — always
  // returns true so the submit proceeds either way.
  window.combinePhone = function (codeId, localId, hiddenId) {
    const code  = document.getElementById(codeId).value;
    const local = document.getElementById(localId).value.trim();
    document.getElementById(hiddenId).value = local ? (code + " " + local) : "";
    return true;
  };

  // Best-effort split of a previously-saved phone string back into
  // {code, local} for populating an Edit modal. Numbers saved before this
  // feature existed (or entered without a "+") can't be reliably attributed
  // to a country, so those fall back to the default code with the whole
  // original string left in the local field — nothing is lost, it just
  // isn't pre-split.
  window.splitPhoneForEdit = function (full, defaultCode) {
    full = (full || "").trim();
    if (!full) return { code: defaultCode, local: "" };
    if (full.startsWith("+")) {
      const byLength = window.PHONE_COUNTRY_CODES.slice().sort(function (a, b) {
        return b.code.length - a.code.length;
      });
      for (const c of byLength) {
        if (full.startsWith(c.code)) {
          return { code: c.code, local: full.slice(c.code.length).trim() };
        }
      }
    }
    return { code: defaultCode, local: full };
  };

  document.addEventListener("DOMContentLoaded", window.populatePhoneCodeSelects);
})();

window.filterTableRows = function (inputId, tableId) {
  const q = document.getElementById(inputId).value.trim().toLowerCase();
  document.querySelectorAll("#" + tableId + " tbody tr").forEach(function (row) {
    const text = row.getAttribute("data-search") || row.textContent.toLowerCase();
    row.style.display = text.includes(q) ? "" : "none";
  });
};

(function () {
  function initMemberPicker(picker) {
    const isMulti = picker.classList.contains("member-picker--multi");
    const input   = picker.querySelector(".member-picker-input");
    const hidden  = picker.querySelector(".member-picker-value");
    const menu    = picker.querySelector(".member-picker-menu");
    const chips   = picker.querySelector(".member-picker-chips");
    const field   = picker.dataset.field || "memberIds";
    const options = Array.from(menu.querySelectorAll(".member-picker-option"));
    const selected = new Map(); // id -> name, multi-select only

    function showMatches() {
      const q = input.value.trim().toLowerCase();
      let anyVisible = false;
      options.forEach(function (opt) {
        const match = !q || opt.dataset.name.toLowerCase().includes(q);
        opt.style.display = match ? "" : "none";
        if (match) anyVisible = true;
      });
      let empty = menu.querySelector(".member-picker-empty");
      if (!anyVisible) {
        if (!empty) {
          empty = document.createElement("div");
          empty.className = "member-picker-empty";
          empty.textContent = "No matches";
          menu.appendChild(empty);
        }
      } else if (empty) {
        empty.remove();
      }
      menu.classList.remove("d-none");
    }

    function choose(opt) {
      if (isMulti) {
        toggleChip(opt.dataset.id, opt.dataset.name);
        opt.classList.toggle("member-picker-option--active", selected.has(opt.dataset.id));
        input.value = "";
        showMatches();
        input.focus();
      } else {
        input.value = opt.dataset.name;
        hidden.value = opt.dataset.id;
        menu.classList.add("d-none");
      }
    }

    function toggleChip(id, name) {
      if (selected.has(id)) {
        selected.delete(id);
        const existing = chips.querySelector('[data-chip-id="' + id + '"]');
        if (existing) existing.remove();
        return;
      }
      selected.set(id, name);
      const chip = document.createElement("span");
      chip.className = "member-picker-chip";
      chip.dataset.chipId = id;
      chip.innerHTML = name.replace(/</g, "&lt;") + ' <i class="fa-solid fa-xmark"></i>' +
        '<input type="hidden" name="' + field + '" value="' + id + '">';
      chip.querySelector("i").addEventListener("click", function () {
        selected.delete(id);
        chip.remove();
        const opt = options.find(function (o) { return o.dataset.id === id; });
        if (opt) opt.classList.remove("member-picker-option--active");
      });
      chips.appendChild(chip);
    }

    input.addEventListener("focus", showMatches);
    input.addEventListener("input", function () {
      if (!isMulti) hidden.value = ""; // typing invalidates whatever was picked before
      showMatches();
    });
    input.addEventListener("keydown", function (e) {
      if (e.key !== "Enter") return;
      const visible = options.filter(function (o) { return o.style.display !== "none"; });
      if (visible.length === 1) {
        e.preventDefault();
        choose(visible[0]);
      } else if (!isMulti && !hidden.value) {
        e.preventDefault(); // don't let an incomplete pick submit the form
      }
    });

    options.forEach(function (opt) {
      opt.addEventListener("click", function () { choose(opt); });
    });

    document.addEventListener("click", function (e) {
      if (!picker.contains(e.target)) menu.classList.add("d-none");
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".member-picker").forEach(initMemberPicker);
  });
})();