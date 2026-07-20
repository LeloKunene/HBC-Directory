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
