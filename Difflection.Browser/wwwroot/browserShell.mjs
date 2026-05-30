export function showBrowserError(document, message) {
    const splash = document.querySelector(".splash");
    if (splash == null) {
        return;
    }

    splash.setAttribute("role", "alert");
    splash.setAttribute("aria-live", "assertive");
    splash.removeAttribute("aria-busy");

    const title = splash.querySelector("strong");
    if (title != null) {
        title.textContent = "Difflection could not start";
    }

    const spans = splash.querySelectorAll("span");
    if (spans.length > 0) {
        spans[0].textContent = message;
        for (let index = 1; index < spans.length; index += 1) {
            spans[index].remove();
        }
    }

    splash.classList.add("splash-error");
}

export function showDropBridgeError(document, message) {
    const dropStatus = document.getElementById("difflection-drop-status");
    if (dropStatus != null) {
        dropStatus.textContent = message;
    }
}
