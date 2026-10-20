// Gevitas Zeiterfassung – kleine, abhängigkeitsfreie Helfer
(function () {
    "use strict";

    // Mobile Navigation ein-/ausblenden
    document.addEventListener("click", function (e) {
        if (e.target.closest("[data-nav-umschalten]")) {
            document.body.classList.toggle("nav-offen");
        } else if (e.target.closest("[data-nav-schliessen]")) {
            document.body.classList.remove("nav-offen");
        }
    });

    // Aktiven Menüpunkt markieren
    var pfad = window.location.pathname.toLowerCase();
    document.querySelectorAll(".nav-link").forEach(function (link) {
        var ziel = (link.getAttribute("href") || "").toLowerCase();
        if ((ziel === "/" && pfad === "/") || (ziel !== "/" && pfad.indexOf(ziel) === 0)) {
            link.classList.add("aktiv");
        }
    });

    // Bestätigungsdialog mit optionaler Bemerkung.
    // Formulare mit data-bestaetigen="Text" werden erst nach "Übernehmen" abgeschickt.
    // Hat das Formular ein Feld name="Bemerkung" (bzw. data-bemerkung-ziel), wird die Bemerkung übernommen.
    var dialog = document.getElementById("bestaetigungsdialog");
    var offenesFormular = null;

    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (!form.dataset || !form.dataset.bestaetigen || form.dataset.bestaetigt === "1" || !dialog || typeof dialog.showModal !== "function") {
            return;
        }
        e.preventDefault();
        offenesFormular = form;
        dialog.querySelector("[data-dialog-titel]").textContent = form.dataset.bestaetigenTitel || "Änderung bestätigen";
        dialog.querySelector("[data-dialog-text]").textContent = form.dataset.bestaetigen;
        var mitBemerkung = form.querySelector("[name='Bemerkung'], [data-bemerkung-ziel]") !== null || document.querySelector("[form='" + form.id + "'][name='Bemerkung']") !== null;
        dialog.querySelector("[data-dialog-bemerkung-feld]").style.display = mitBemerkung ? "" : "none";
        dialog.querySelector("[data-dialog-bemerkung]").value = "";
        dialog.showModal();
    });

    if (dialog) {
        dialog.querySelector("[data-dialog-abbrechen]").addEventListener("click", function () {
            offenesFormular = null;
            dialog.close();
        });
        dialog.querySelector("[data-dialog-ok]").addEventListener("click", function () {
            if (!offenesFormular) { dialog.close(); return; }
            var text = dialog.querySelector("[data-dialog-bemerkung]").value;
            var ziel = offenesFormular.querySelector("[name='Bemerkung'], [data-bemerkung-ziel]")
                || (offenesFormular.id ? document.querySelector("[form='" + offenesFormular.id + "'][name='Bemerkung']") : null);
            if (ziel && !ziel.value) { ziel.value = text; }
            offenesFormular.dataset.bestaetigt = "1";
            dialog.close();
            if (typeof offenesFormular.requestSubmit === "function") {
                offenesFormular.requestSubmit();
            } else {
                offenesFormular.submit();
            }
        });
    }

    // Auswahlfelder, die ihr Formular sofort absenden (statt Inline-Handler wegen CSP)
    document.addEventListener("change", function (e) {
        if (e.target.matches && e.target.matches("[data-auto-absenden]") && e.target.form) {
            e.target.form.submit();
        }
    });

    // Live-Uhr im Terminal
    var uhr = document.querySelector("[data-live-uhr]");
    if (uhr) {
        var aktualisieren = function () {
            var jetzt = new Date();
            uhr.textContent = jetzt.toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
        };
        aktualisieren();
        setInterval(aktualisieren, 1000);
    }
})();
